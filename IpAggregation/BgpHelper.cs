namespace IpAggregation
{
    /// <summary>
    /// A helper class for BGP.
    /// </summary>
    public static class BgpHelper
    {
        private const string OriginIncomplete = "Incomplete";
        private const string OriginEgp = "Egp";

        /// <summary>
        /// A helper function that generates an aggregate, given two
        /// route contributors.
        /// Currently assumes the route contributors are adjacents.
        /// The implementation is derived from RFC 4721.
        /// Please note that the RFC states that the nexthop should
        /// be an interface of the BGP speaker aggregating the route,
        /// in case the two contributing routes have different nexthops
        /// (section 9.2.2.2).
        /// The current implementation, however, always assigns the
        /// nexthop of the aggregate to be the nexthop of the first
        /// parameter given to this function. This is to avoid any case
        /// of production traffic forwarding towards the BGP speaker.
        /// </summary>
        /// <param name="route">The route entry</param>
        /// <param name="adjRoute">The adjacent route</param>
        /// <param name="logger">The logger.</param>
        /// <param name="aggregateRoute">The aggregate route</param>
        /// <returns>True if the routes were aggregated</returns>
        public static bool TryGenerateAggregate(
            BgpPrefix route,
            BgpPrefix adjRoute,
            out BgpPrefix aggregateRoute)
        {
            aggregateRoute = null;

            // As defined in https://tools.ietf.org/html/rfc4271#section-9.2.2.2
            // we don't aggregated if MED is different.
            if (route.Med != adjRoute.Med)
            {
                return false;
            }

            // (only IP unicast is supported at this point)

            // ORIGIN
            string origin;
            if (route.Origin == OriginIncomplete ||
                adjRoute.Origin == OriginIncomplete)
            {
                origin = OriginIncomplete;
            }
            else if (route.Origin == OriginEgp ||
                adjRoute.Origin == OriginEgp)
            {
                origin = OriginEgp;
            }
            else
            {
                origin = "Igp";
            }

            // AS_PATH: calculate the AS path. (not supported at this point)

            // ATOMIC_AGGREGATE, true if any of the specifics has it.
            var atomicAggregate = false;
            if ((route.AtomicAggregate.HasValue &&
                route.AtomicAggregate.Value) ||
                (adjRoute.AtomicAggregate.HasValue &&
                adjRoute.AtomicAggregate.Value))
            {
                atomicAggregate = true;
            }

            // Perform union of the communities.
            var allComunities = route.Community.Union(
                adjRoute.Community).ToList();

            // Perform union of the extended communities. (not supported at this point)

            // Finally, create and returns the BgpRoute.
            aggregateRoute = new BgpPrefix(
                route.Address, route.MaskLength - 1, allComunities)
            {
                Med = route.Med,
                Origin = origin,
                AtomicAggregate = atomicAggregate
            };

            return true;
        }
    }
}
