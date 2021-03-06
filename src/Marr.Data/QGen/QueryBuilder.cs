using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Marr.Data.Mapping;
using System.Data.Common;
using System.Collections;
using Marr.Data.QGen.Dialects;

namespace Marr.Data.QGen
{
    /// <summary>
    /// This class is responsible for building a select query.
    /// It uses chaining methods to provide a fluent interface for creating select queries.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryBuilder<T> : ExpressionVisitor, IEnumerable<T>, IQueryBuilder
    {
        #region - Private Members -

        private DataMapper _db;
        private Dialect _dialect;
        private TableCollection _tables;
        private WhereBuilder<T> _whereBuilder;
        private SortBuilder<T> _sortBuilder;
        private bool _isGraph = false;
        private bool _isFromView = false;
        private bool _isFromTable = false;
        private bool _isJoin = false;
        private bool _isManualQuery = false;
        private bool _enablePaging = false;
        private int _skip;
        private int _take;
        private string _queryText;
        private List<MemberInfo> _childrenToLoad;
        private SortBuilder<T> SortBuilder
        {
            get
            {
                // Lazy load
                if (_sortBuilder == null)
                {
                    bool useAltNames = _isFromView || _isGraph || _isJoin;
                    _sortBuilder = new SortBuilder<T>(this, _db, _whereBuilder, _dialect, _tables, useAltNames);
                }

                return _sortBuilder;
            }
        }
        private List<T> _results = new List<T>();
        private EntityGraph _entityGraph;
        private EntityGraph EntGraph
        {
            get
            {
                if (_entityGraph == null)
                {
                    _entityGraph = new EntityGraph(typeof(T), _results);
                }

                return _entityGraph;
            }
        }

        #endregion

        #region - Constructor -

        public QueryBuilder()
        {
            // Used only for unit testing with mock frameworks
        }

        public QueryBuilder(DataMapper db, Dialect dialect)
        {
            _db = db;
            _dialect = dialect;
            _tables = new TableCollection();
            _tables.Add(new Table(typeof(T)));
            _childrenToLoad = new List<MemberInfo>();
        }

        #endregion

        #region - Fluent Methods -

        /// <summary>
        /// Overrides the base table name that will be used in the query.
        /// </summary>
        [Obsolete("This method is obsolete.  Use either the FromTable or FromView method instead.", true)]
        public virtual QueryBuilder<T> From(string tableName)
        {
            return FromView(tableName);
        }

        /// <summary>
        /// Overrides the base view name that will be used in the query.
        /// Will try to use the mapped "AltName" values when loading the columns.
        /// </summary>
        public virtual QueryBuilder<T> FromView(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
                throw new ArgumentNullException("view");

            _isFromView = true;

            // Replace the base table with a view with tables
            if (_tables[0] is View)
            {
                (_tables[0] as View).Name = viewName;
            }
            else
            {
                View view = new View(viewName, _tables.ToArray());
                _tables.ReplaceBaseTable(view);
            }

            return this;
        }

        /// <summary>
        /// Overrides the base table name that will be used in the query.
        /// Will not try to use the mapped "AltName" values when loading the  columns.
        /// </summary>
        public virtual QueryBuilder<T> FromTable(string table)
        {
            if (string.IsNullOrEmpty(table))
                throw new ArgumentNullException("view");

            _isFromTable = true;

            // Override the base table name
            _tables[0].Name = table;
            return this;
        }

        /// <summary>
        /// Allows you to manually specify the query text.
        /// </summary>
        public virtual QueryBuilder<T> QueryText(string queryText)
        {
            _isManualQuery = true;
            _queryText = queryText;
            return this;
        }

        /// <summary>
        /// If no parameters are passed in, this method instructs the DataMapper to load all related entities in the graph.
        /// If specific entities are passed in, only these relationships will be loaded.
        /// </summary>
        /// <param name="childrenToLoad">A list of related child entites to load (passed in as properties / lambda expressions).</param>
        public virtual QueryBuilder<T> Graph(params Expression<Func<T, object>>[] childrenToLoad)
        {
            TableCollection tablesInView = new TableCollection();
            if (childrenToLoad.Length > 0)
            {
                // Add base table
                tablesInView.Add(_tables[0]);

                foreach (var exp in childrenToLoad)
                {
                    MemberInfo child = (exp.Body as MemberExpression).Member;

                    var node = EntGraph.Where(g => g.Member != null && g.Member.EqualsMember(child)).FirstOrDefault();
                    if (node != null)
                    {
                        tablesInView.Add(new Table(node.EntityType, JoinType.None));
                    }

                    if (!_childrenToLoad.ContainsMember(child))
                    {
                        _childrenToLoad.Add(child);
                    }
                }
            }
            else
            {
                // Add all tables in the graph
                foreach (var node in EntGraph)
                {
                    tablesInView.Add(new Table(node.EntityType, JoinType.None));
                }
            }

            // Replace the base table with a view with tables
            View view = new View(_tables[0].Name, tablesInView.ToArray());
            _tables.ReplaceBaseTable(view);

            _isGraph = true;
            return this;
        }
        
        public virtual QueryBuilder<T> Page(int pageNumber, int pageSize)
        {
            _enablePaging = true;
            _skip = (pageNumber - 1) * pageSize;
            _take = pageSize;
            return this;
        }
        
        private string[] ParseChildrenToLoad(Expression<Func<T, object>>[] childrenToLoad)
        {
            List<string> entitiesToLoad = new List<string>();

            // Parse relationship member names from expression array
            foreach (var exp in childrenToLoad)
            {
                MemberInfo member = (exp.Body as MemberExpression).Member;
                entitiesToLoad.Add(member.Name);
                
            }

            return entitiesToLoad.ToArray();
        }

        /// <summary>
        /// Allows you to interact with the DbDataReader to manually load entities.
        /// </summary>
        /// <param name="readerAction">An action that takes a DbDataReader.</param>
        public virtual void DataReader(Action<DbDataReader> readerAction)
        {
            if (string.IsNullOrEmpty(_queryText))
                throw new ArgumentNullException("The query text cannot be blank.");

            var mappingHelper = new MappingHelper(_db);
            _db.Command.CommandText = _queryText;

            try
            {
                _db.OpenConnection();
                using (DbDataReader reader = _db.Command.ExecuteReader())
                {
                    readerAction.Invoke(reader);
                }
            }
            finally
            {
                _db.CloseConnection();
            }
        }

        public virtual int GetRowCount()
        {
            SqlModes previousSqlMode = _db.SqlMode;

            // Generate a row count query
            string where = _whereBuilder != null ? _whereBuilder.ToString() : string.Empty;

            bool useAltNames = _isFromView || _isGraph || _isJoin;
            IQuery query = QueryFactory.CreateRowCountSelectQuery(_tables, _db, where, SortBuilder, useAltNames);
            string queryText = query.Generate();

            _db.SqlMode = SqlModes.Text;
            int count = Convert.ToInt32(_db.ExecuteScalar(queryText));

            _db.SqlMode = previousSqlMode;
            return count;
        }

        /// <summary>
        /// Executes the query and returns a list of results.
        /// </summary>
        /// <returns>A list of query results of type T.</returns>
        public virtual List<T> ToList()
        {
            SqlModes previousSqlMode = _db.SqlMode;

            ValidateQuery();

            BuildQueryOrAppendClauses();

            if (_isGraph || _isJoin)
            {
                _results = (List<T>)_db.QueryToGraph<T>(_queryText, EntGraph, _childrenToLoad);
            }
            else
            {
                _results = (List<T>)_db.Query<T>(_queryText, _results, _isFromView);
            }

            // Return to previous sql mode
            _db.SqlMode = previousSqlMode;

            return _results;
        }

        private void ValidateQuery()
        {
            if (_isManualQuery && _isFromView)
                throw new InvalidOperationException("Cannot use FromView in conjunction with QueryText");

            if (_isManualQuery && _isFromTable)
                throw new InvalidOperationException("Cannot use FromTable in conjunction with QueryText");

            if (_isManualQuery && _isJoin)
                throw new InvalidOperationException("Cannot use Join in conjuntion with QueryText");

            if (_isManualQuery && _enablePaging)
                throw new InvalidOperationException("Cannot use Page, Skip or Take in conjunction with QueryText");

            if (_isJoin && _isFromView)
                throw new InvalidOperationException("Cannot use FromView in conjunction with Join");

            if (_isJoin && _isFromTable)
                throw new InvalidOperationException("Cannot use FromView in conjunction with Join");

            if (_isJoin && _isGraph)
                throw new InvalidOperationException("Cannot use Graph in conjunction with Join");

            if (_isFromView && _isFromTable)
                throw new InvalidOperationException("Cannot use FromView in conjunction with FromTable");
        }

        private void BuildQueryOrAppendClauses()
        {
            if (_queryText == null)
            {
                // Build entire query
                _db.SqlMode = SqlModes.Text;
                BuildQuery();
            }
            else if (_whereBuilder != null || _sortBuilder != null)
            {
                _db.SqlMode = SqlModes.Text;
                if (_whereBuilder != null)
                {
                    // Append a where clause to an existing query
                    _queryText = string.Concat(_queryText, " ", _whereBuilder.ToString());
                }

                if (_sortBuilder != null)
                {
                    // Append an order clause to an existing query
                    _queryText = string.Concat(_queryText, " ", _sortBuilder.ToString());
                }
            }
        }

        public virtual string BuildQuery()
        {
            // Generate a query
            string where = _whereBuilder != null ? _whereBuilder.ToString() : string.Empty;

            bool useAltNames = _isFromView || _isGraph || _isJoin;

            IQuery query = null;
            if (_enablePaging)
            {
                query = QueryFactory.CreatePagingSelectQuery(_tables, _db, where, SortBuilder, useAltNames, _skip, _take);
            }
            else
            {
                query = QueryFactory.CreateSelectQuery(_tables, _db, where, SortBuilder, useAltNames);
            }

            _queryText = query.Generate();

            return _queryText;
        }

        #endregion

        #region - Helper Methods -

        private ColumnMapCollection GetColumns(IEnumerable<string> entitiesToLoad)
        {
            // If QueryToGraph<T> and no child load entities are specified, load all children
            bool useAltNames = _isFromView || _isGraph || _isJoin;
            bool loadAllChildren = useAltNames && entitiesToLoad == null;

            // If Query<T>
            if (!useAltNames)
            {
                return MapRepository.Instance.GetColumns(typeof(T));
            }

            ColumnMapCollection columns = new ColumnMapCollection();

            Type baseEntityType = typeof(T);
            EntityGraph graph = new EntityGraph(baseEntityType, null);

            foreach (var lvl in graph)
            {
                if (loadAllChildren || lvl.IsRoot || entitiesToLoad.Contains(lvl.Member.Name))
                {
                    columns.AddRange(lvl.Columns);
                }
            }

            return columns;
        }

        public static implicit operator List<T>(QueryBuilder<T> builder)
        {
            return builder.ToList();
        }

        #endregion

        #region - Linq Support -

        public virtual SortBuilder<T> Where<TObj>(Expression<Func<TObj, bool>> filterExpression)
        {
            bool useAltNames = _isFromView || _isGraph;
            bool addTablePrefixToColumns = true;
            _whereBuilder = new WhereBuilder<T>(_db.Command, _dialect, filterExpression, _tables, useAltNames, addTablePrefixToColumns);
            return SortBuilder;
        }

        public virtual SortBuilder<T> Where(Expression<Func<T, bool>> filterExpression)
        {
            bool useAltNames = _isFromView || _isGraph;
            bool addTablePrefixToColumns = true;
            _whereBuilder = new WhereBuilder<T>(_db.Command, _dialect, filterExpression, _tables, useAltNames, addTablePrefixToColumns);
            return SortBuilder;
        }

        public virtual SortBuilder<T> Where(string whereClause)
        {
            if (string.IsNullOrEmpty(whereClause))
                throw new ArgumentNullException("whereClause");

            if (!whereClause.ToUpper().Contains("WHERE "))
            {
                whereClause = whereClause.Insert(0, " WHERE ");
            }

            bool useAltNames = _isFromView || _isGraph || _isJoin;
            _whereBuilder = new WhereBuilder<T>(whereClause, useAltNames);
            return SortBuilder;
        }

        public virtual SortBuilder<T> OrderBy(Expression<Func<T, object>> sortExpression)
        {
            SortBuilder.OrderBy(sortExpression);
            return SortBuilder;
        }

        public virtual SortBuilder<T> OrderBy(Expression<Func<T, object>> sortExpression, SortDirection sortDirection)
        {
            SortBuilder.OrderBy(sortExpression, sortDirection);
            return SortBuilder;
        }

        public virtual SortBuilder<T> ThenBy(Expression<Func<T, object>> sortExpression)
        {
            SortBuilder.OrderBy(sortExpression);
            return SortBuilder;
        }

        public virtual SortBuilder<T> ThenBy(Expression<Func<T, object>> sortExpression, SortDirection sortDirection)
        {
            SortBuilder.OrderBy(sortExpression, sortDirection);
            return SortBuilder;
        }

        public virtual SortBuilder<T> OrderByDescending(Expression<Func<T, object>> sortExpression)
        {
            SortBuilder.OrderByDescending(sortExpression);
            return SortBuilder;
        }

        public virtual SortBuilder<T> ThenByDescending(Expression<Func<T, object>> sortExpression)
        {
            SortBuilder.OrderByDescending(sortExpression);
            return SortBuilder;
        }

        public virtual SortBuilder<T> OrderBy(string orderByClause)
        {
            if (string.IsNullOrEmpty(orderByClause))
                throw new ArgumentNullException("orderByClause");

            if (!orderByClause.ToUpper().Contains("ORDER BY "))
            {
                orderByClause = orderByClause.Insert(0, " ORDER BY ");
            }

            SortBuilder.OrderBy(orderByClause);
            return SortBuilder;
        }

        public virtual QueryBuilder<T> Take(int count)
        {
            _enablePaging = true;
            _take = count;
            return this;
        }

        public virtual QueryBuilder<T> Skip(int count)
        {
            _enablePaging = true;
            _skip = count;
            return this;
        }

        /// <summary>
        /// Handles all.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override Expression Visit(Expression expression)
        {
            return base.Visit(expression);
        }

        /// <summary>
        /// Handles Where.
        /// </summary>
        /// <param name="lambdaExpression"></param>
        /// <returns></returns>
        protected override Expression VisitLamda(LambdaExpression lambdaExpression)
        {
            _sortBuilder = Where(lambdaExpression as Expression<Func<T, bool>>);
            return base.VisitLamda(lambdaExpression);
        }

        /// <summary>
        /// Handles OrderBy.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            if (expression.Method.Name == "OrderBy" || expression.Method.Name == "ThenBy")
            {
                var memberExp = ((expression.Arguments[1] as UnaryExpression).Operand as LambdaExpression).Body as MemberExpression;
                _sortBuilder.Order(memberExp.Expression.Type, memberExp.Member.Name);
            }
            if (expression.Method.Name == "OrderByDescending" || expression.Method.Name == "ThenByDescending")
            {
                var memberExp = ((expression.Arguments[1] as UnaryExpression).Operand as LambdaExpression).Body as MemberExpression;
                _sortBuilder.OrderByDescending(memberExp.Expression.Type, memberExp.Member.Name);
            }

            return base.VisitMethodCall(expression);
        }

        public virtual QueryBuilder<T> Join<TLeft, TRight>(JoinType joinType, Expression<Func<TLeft, IEnumerable<TRight>>> rightEntity, Expression<Func<TLeft, TRight, bool>> filterExpression)
        {
            _isJoin = true;
            MemberInfo rightMember = (rightEntity.Body as MemberExpression).Member;
            return Join(joinType, rightMember, filterExpression);
        }

        public virtual QueryBuilder<T> Join<TLeft, TRight>(JoinType joinType, Expression<Func<TLeft, TRight>> rightEntity, Expression<Func<TLeft, TRight, bool>> filterExpression)
        {
            _isJoin = true;
            MemberInfo rightMember = (rightEntity.Body as MemberExpression).Member;
            return Join(joinType, rightMember, filterExpression);
        }

        public virtual QueryBuilder<T> Join<TLeft, TRight>(JoinType joinType, Expression<Func<TLeft, LazyLoaded<TRight>>> rightEntity, Expression<Func<TLeft, TRight, bool>> filterExpression)
        {
            _isJoin = true;
            MemberInfo rightMember = (rightEntity.Body as MemberExpression).Member;

            foreach (var item in EntGraph)
            {
                if (item.EntityType == typeof(TLeft))
                {
                    var relationship = item.Relationships.Single(v => v.Member == rightMember);
                    item.AddLazyRelationship(relationship);
                }
            }

            return Join(joinType, rightMember, filterExpression);
        }

        public virtual QueryBuilder<T> Join<TLeft, TRight>(JoinType joinType, MemberInfo rightMember, Expression<Func<TLeft, TRight, bool>> filterExpression)
        {
            _isJoin = true;

            if (!_childrenToLoad.ContainsMember(rightMember))
                _childrenToLoad.Add(rightMember);

            Table table = new Table(typeof(TRight), joinType);
            _tables.Add(table);

            var builder = new JoinBuilder<TLeft, TRight>(_db.Command, _dialect, filterExpression, _tables);

            table.JoinClause = builder.ToString();
            return this;
        }

        public virtual bool Any(Expression<Func<T, bool>> filterExpression)
        {
            bool useAltNames = _isFromView || _isGraph;
            bool addTablePrefixToColumns = true;
            _whereBuilder = new WhereBuilder<T>(_db.Command, _dialect, filterExpression, _tables, useAltNames, addTablePrefixToColumns);
            return Any();
        }

        public virtual bool Any()
        {
            SqlModes previousSqlMode = _db.SqlMode;

            // Generate a row count query
            string where = _whereBuilder != null ? _whereBuilder.ToString() : string.Empty;

            bool useAltNames = _isFromView || _isGraph || _isJoin;
            IQuery query = QueryFactory.CreateRowCountSelectQuery(_tables, _db, where, SortBuilder, useAltNames);
            string queryText = query.Generate();

            _db.SqlMode = SqlModes.Text;
            int count = Convert.ToInt32(_db.ExecuteScalar(queryText));

            _db.SqlMode = previousSqlMode;
            return count > 0;
        }

        #endregion

        #region IEnumerable<T> Members

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            var list = ToList();
            return list.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            var list = ToList();
            return list.GetEnumerator();
        }

        #endregion
    }
}
