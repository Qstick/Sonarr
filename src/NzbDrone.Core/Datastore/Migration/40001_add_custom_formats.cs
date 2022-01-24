using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(400001)]
    public class add_custom_formats : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            //Add Custom Format Columns
            Create.TableForModel("CustomFormats")
                .WithColumn("Name").AsString().Unique()
                .WithColumn("Specifications").AsString().WithDefaultValue("[]")
                .WithColumn("IncludeCustomFormatWhenRenaming").AsBoolean().WithDefaultValue(false);

            //Add Custom Format Columns to Quality Profiles
            Alter.Table("QualityProfiles").AddColumn("FormatItems").AsString().WithDefaultValue("[]");
            Alter.Table("QualityProfiles").AddColumn("MinFormatScore").AsInt32().WithDefaultValue(0);
            Alter.Table("QualityProfiles").AddColumn("CutoffFormatScore").AsInt32().WithDefaultValue(0);

            //Migrate Preferred Words to Custom Formats
            Execute.WithConnection(MigratePreferredTerms);

            //Remove Preferred Word Columns from ReleaseProfiles
            Delete.Column("Preferred").FromTable("ReleaseProfiles");
            Delete.Column("IncludePreferredWhenRenaming").FromTable("ReleaseProfiles");

            //Remove Profiles that will no longer validate
            Execute.Sql("DELETE FROM ReleaseProfiles WHERE Required == '[]' AND Ignored == '[]'");

            //TODO: Kill any references to Preferred in History and Files
            //Data.PreferredWordScore
        }

        private void MigratePreferredTerms(IDbConnection conn, IDbTransaction tran)
        {
            var updatedCollections = new List<CustomFormat171>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = "SELECT Preferred, Name, IncludePreferredWhenRenaming FROM ReleaseProfiles WHERE Preferred IS NOT NULL AND Enabled == true";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var preferred = reader.GetString(0);
                        var nameObj = reader.GetValue(1);
                        var includeName = reader.GetBoolean(2);

                        var name = nameObj == DBNull.Value ? null : (string)nameObj;

                        var data = STJson.Deserialize<List<PreferredWord170>>(preferred);

                        var specs = new List<CustomFormatSpec171>();

                        foreach (var term in data)
                        {
                            specs.Add(new CustomFormatSpec171
                            {
                                Type = "ReleaseTitleSpecification",
                                Body = new CustomFormatReleaseTitleSpec171
                                {
                                    Order = 1,
                                    ImplementationName = "Release Title",
                                    Name = term.Key,
                                    Value = term.Key
                                }
                            });
                        }

                        if (specs.Count > 0)
                        {
                            updatedCollections.Add(new CustomFormat171
                            {
                                Name = name ?? data.First().Key,
                                IncludeCustomFormatWhenRenaming = includeName,
                                Specifications = specs.ToJson()
                            });
                        }
                    }
                }
            }

            var updateSql = "INSERT INTO CustomFormats (Name, IncludeCustomFormatWhenRenaming, Specifications) VALUES (@Name, @IncludeCustomFormatWhenRenaming, @Specifications)";
            conn.Execute(updateSql, updatedCollections, transaction: tran);
        }

        private class PreferredWord170
        {
            public string Key { get; set; }
            public int Value { get; set; }
        }

        private class CustomFormat171
        {
            public string Name { get; set; }
            public bool IncludeCustomFormatWhenRenaming { get; set; }
            public string Specifications { get; set; }
        }

        private class CustomFormatSpec171
        {
            public string Type { get; set; }
            public CustomFormatReleaseTitleSpec171 Body { get; set; }
        }

        private class CustomFormatReleaseTitleSpec171
        {
            public int Order { get; set; }
            public string ImplementationName { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public bool Required { get; set; }
            public bool Negate { get; set; }
        }
    }
}
