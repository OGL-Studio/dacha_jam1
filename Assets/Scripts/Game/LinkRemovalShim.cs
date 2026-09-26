using System;
using System.Collections;
using System.Reflection;
using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Removes an existing link from the simulation's graph and refunds part of its price.
    /// Story 003 acceptance criterion 3 ("ПКМ по связи удаляет её с частичным возвратом").
    /// </summary>
    /// <remarks>
    /// <para><b>This class is a shim and should not exist.</b> <c>NightShift.Core</c> has no
    /// link-removal concept at all: <see cref="NetworkGraph"/> exposes <c>TryAddLink</c> with no
    /// counterpart, its link and adjacency collections are private, and
    /// <see cref="NetworkSimulation.Credits"/> has a private setter with no public credit-award
    /// method. The correct fix is a <c>NetworkGraph.TryRemoveLink</c> plus a credit-aware
    /// <c>NetworkSimulation.TryRemoveLink</c> in Core, next to the other build actions and covered
    /// by Core's own tests. Story 003 explicitly does not own <c>Assets/Scripts/Core/</c>, so until
    /// that API lands the removal is performed here by reflecting into Core's private state.</para>
    ///
    /// <para><b>Why reflection and not a rebuild.</b> The alternative that needs no reflection is to
    /// throw the <see cref="NetworkSimulation"/> away and replay every surviving build action into a
    /// fresh one. That resets <see cref="NetworkSimulation.CoreIntegrity"/> to full (also a private
    /// setter), which would turn every link removal into a free repair of the Core - a worse defect
    /// than this one, and a silent one.</para>
    ///
    /// <para><b>Failure is handled, not assumed away.</b> Everything is resolved once and
    /// <see cref="IsAvailable"/> reports the result, so a Core refactor that renames a field
    /// degrades to "removal unavailable" with an error in the log instead of a crash. Managed code
    /// stripping could in principle break this in an IL2CPP build; the fields are read and written
    /// by Core itself, so they survive stripping, but if this ever regresses in a player build,
    /// that is the first thing to check.</para>
    /// </remarks>
    public static class LinkRemovalShim
    {
        private const string LinksByKeyFieldName = "_linksByKey";
        private const string LinksFieldName = "_links";
        private const string AdjacencyFieldName = "_adjacency";
        private const string CreditsPropertyName = "Credits";
        private const string CreditsChangedEventName = "OnCreditsChanged";

        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private static FieldInfo _linksByKeyField;
        private static FieldInfo _linksField;
        private static FieldInfo _adjacencyField;
        private static FieldInfo _creditsChangedField;
        private static MethodInfo _creditsSetter;

        private static bool _resolved;
        private static bool _available;

        /// <summary>
        /// True when every private member this shim needs was found, i.e. removal will work. False
        /// after a Core change broke it - callers should then refuse the interaction rather than
        /// pretend it succeeded.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                EnsureResolved();
                return _available;
            }
        }

        /// <summary>
        /// Removes <paramref name="link"/> and credits <paramref name="refund"/>, raising
        /// <see cref="NetworkSimulation.OnCreditsChanged"/> exactly as Core's own build actions do.
        /// </summary>
        /// <param name="simulation">Simulation owning the link.</param>
        /// <param name="link">Link to remove. Only its endpoints matter - <see cref="Link.Length"/> is not part of link identity.</param>
        /// <param name="refund">Credits to return. Never negative; the caller derives it from the link's price.</param>
        /// <param name="error">Reason the removal failed, or null on success.</param>
        /// <returns>True when the link is gone and the refund has been paid.</returns>
        public static bool TryRemoveLink(NetworkSimulation simulation, Link link, float refund, out string error)
        {
            if (simulation == null)
            {
                error = "Simulation is null.";
                return false;
            }

            EnsureResolved();
            if (!_available)
            {
                error = "Link removal is unavailable: NightShift.Core exposes no removal API and the " +
                        "compatibility shim could not bind to its internals.";
                return false;
            }

            if (!simulation.Graph.TryGetLink(link.NodeAId, link.NodeBId, out Link existing))
            {
                error = "No such link.";
                return false;
            }

            try
            {
                NetworkGraph graph = simulation.Graph;

                var linksByKey = (IDictionary)_linksByKeyField.GetValue(graph);
                var links = (IList)_linksField.GetValue(graph);
                var adjacency = (IDictionary)_adjacencyField.GetValue(graph);

                linksByKey.Remove(existing);

                int index = links.IndexOf(existing);
                if (index >= 0)
                {
                    links.RemoveAt(index);
                }

                RemoveAdjacency(adjacency, existing.NodeAId, existing.NodeBId);
                RemoveAdjacency(adjacency, existing.NodeBId, existing.NodeAId);

                if (refund > 0f)
                {
                    float credits = simulation.Credits + refund;
                    _creditsSetter.Invoke(simulation, new object[] { credits });
                    RaiseCreditsChanged(simulation, credits);
                }
            }
            catch (Exception exception)
            {
                _available = false;
                Debug.LogError("[NightShift] Link removal shim failed against NightShift.Core: " + exception);
                error = "Link removal failed: " + exception.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static void RemoveAdjacency(IDictionary adjacency, int fromId, int toId)
        {
            if (adjacency[fromId] is IList neighbors)
            {
                neighbors.Remove(toId);
            }
        }

        private static void RaiseCreditsChanged(NetworkSimulation simulation, float credits)
        {
            if (_creditsChangedField == null)
            {
                return;
            }

            if (_creditsChangedField.GetValue(simulation) is Action<float> handler)
            {
                handler(credits);
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }
            _resolved = true;

            Type graphType = typeof(NetworkGraph);
            Type simulationType = typeof(NetworkSimulation);

            _linksByKeyField = graphType.GetField(LinksByKeyFieldName, InstanceNonPublic);
            _linksField = graphType.GetField(LinksFieldName, InstanceNonPublic);
            _adjacencyField = graphType.GetField(AdjacencyFieldName, InstanceNonPublic);

            PropertyInfo creditsProperty = simulationType.GetProperty(CreditsPropertyName);
            _creditsSetter = creditsProperty != null ? creditsProperty.GetSetMethod(true) : null;

            // A field-like event's backing field carries the event's own name and is private.
            _creditsChangedField = simulationType.GetField(CreditsChangedEventName, InstanceNonPublic);

            _available = _linksByKeyField != null &&
                         _linksField != null &&
                         _adjacencyField != null &&
                         _creditsSetter != null;

            if (!_available)
            {
                Debug.LogError(
                    "[NightShift] LinkRemovalShim could not bind to NightShift.Core internals " +
                    "(links=" + (_linksField != null) +
                    ", linksByKey=" + (_linksByKeyField != null) +
                    ", adjacency=" + (_adjacencyField != null) +
                    ", creditsSetter=" + (_creditsSetter != null) +
                    "). Link removal is disabled. Add NetworkGraph.TryRemoveLink to Core and delete this shim.");
            }
        }
    }
}
