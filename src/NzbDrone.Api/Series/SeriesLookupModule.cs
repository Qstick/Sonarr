using System.Collections.Generic;
using Nancy;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource;
using System.Linq;
using Sonarr.Http;

namespace NzbDrone.Api.Series
{
    public class SeriesLookupModule : SonarrRestModule<SeriesResource>
    {
        private readonly ISearchForNewSeries _searchProxy;

        public SeriesLookupModule(ISearchForNewSeries searchProxy)
            : base("/series/lookup")
        {
            _searchProxy = searchProxy;
            Get("/",  x => Search());
        }


        private object Search()
        {
            var tvDbResults = _searchProxy.SearchForNewSeries((string)Request.Query.term);
            return MapToResource(tvDbResults);
        }


        private static IEnumerable<SeriesResource> MapToResource(IEnumerable<Core.Tv.Series> series)
        {
            foreach (var currentSeries in series)
            {
                var resource = currentSeries.ToResource();
                var poster = currentSeries.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);
                if (poster != null)
                {
                    resource.RemotePoster = poster.Url;
                }

                yield return resource;
            }
        }
    }
}