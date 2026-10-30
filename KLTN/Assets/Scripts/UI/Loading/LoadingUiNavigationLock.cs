using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EchoProtocol.UI.Loading
{
    /// <summary>Shares the original navigation state across overlapping loading presenters.</summary>
    public sealed class LoadingUiNavigationLock
    {
        private sealed class NavigationState
        {
            public readonly bool SendNavigationEvents;
            public readonly GameObject Selection;
            public readonly HashSet<LoadingUiNavigationLock> Owners = new HashSet<LoadingUiNavigationLock>();

            public NavigationState(EventSystem events)
            {
                SendNavigationEvents = events.sendNavigationEvents;
                Selection = events.currentSelectedGameObject;
            }
        }

        private static readonly Dictionary<EventSystem, NavigationState> States =
            new Dictionary<EventSystem, NavigationState>();
        private EventSystem _events;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            var previous = new List<KeyValuePair<EventSystem, NavigationState>>(States);
            States.Clear();
            foreach (var entry in previous)
            {
                foreach (var owner in entry.Value.Owners) owner._events = null;
                Restore(entry.Key, entry.Value);
            }
        }

        public void Acquire(EventSystem events)
        {
            if (events == null) { Release(); return; }
            if (!ReferenceEquals(_events, events))
            {
                Release();
                if (!States.TryGetValue(events, out var state))
                {
                    state = new NavigationState(events);
                    States.Add(events, state);
                }
                state.Owners.Add(this);
                _events = events;
            }
            Block(events);
        }

        public void Release()
        {
            var events = _events;
            _events = null;
            // Unity's destroyed object compares equal to null, but still owns a dictionary entry.
            if (ReferenceEquals(events, null) || !States.TryGetValue(events, out var state)) return;
            state.Owners.Remove(this);
            if (state.Owners.Count > 0)
            {
                if (events != null) Block(events);
                return;
            }
            States.Remove(events);
            Restore(events, state);
        }

        private static void Block(EventSystem events)
        {
            events.sendNavigationEvents = false;
            if (events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);
        }

        private static void Restore(EventSystem events, NavigationState state)
        {
            if (events == null) return;
            events.sendNavigationEvents = state.SendNavigationEvents;
            if (state.Selection != null && state.Selection.activeInHierarchy)
                events.SetSelectedGameObject(state.Selection);
        }
    }
}
