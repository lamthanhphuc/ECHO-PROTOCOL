using System;
using System.Collections.Generic;
using EchoProtocol.Api;
using EchoProtocol.Auth;

namespace EchoProtocol.UI.MainMenu
{
    public static class TeamToolOwnershipSession
    {
        [Serializable]
        private sealed class InventoryResponse
        {
            public bool success;
            public InventoryData data;
        }

        [Serializable]
        private sealed class InventoryData
        {
            public OwnedItem[] items;
        }

        [Serializable]
        private sealed class OwnedItem
        {
            public string itemId;
        }

        private static readonly HashSet<string> OwnedItemIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        public static event Action Changed;

        private static bool _isLoaded;
        private static bool _isLoading;
        public static bool IsLoaded
        {
            get { EnsureCurrentUser(); return _isLoaded; }
            private set => _isLoaded = value;
        }
        public static bool IsLoading
        {
            get { EnsureCurrentUser(); return _isLoading; }
            private set => _isLoading = value;
        }
        private static string _sessionUserId = string.Empty;
        private static int _revision;

        private static void EnsureCurrentUser()
        {
            var currentUserId = AuthSession.CurrentUserId;
            if (!string.Equals(_sessionUserId, currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                Clear();
                _sessionUserId = currentUserId;
            }
        }

        public static void Clear()
        {
            _revision++;
            _sessionUserId = AuthSession.CurrentUserId;
            OwnedItemIds.Clear();
            IsLoaded = false;
            IsLoading = false;
            Changed?.Invoke();
        }

        public static bool OwnsItem(
            string itemId)
        {
            EnsureCurrentUser();
            return !string.IsNullOrWhiteSpace(itemId)
                && OwnedItemIds.Contains(itemId);
        }

        public static bool OwnsTool(
            int toolId)
        {
            return TeamToolShopAssets.TryGetByToolId(
                    toolId,
                    out var item)
                && OwnsItem(item.ItemId);
        }

        public static void MarkOwned(
            string itemId)
        {
            EnsureCurrentUser();
            if (string.IsNullOrEmpty(_sessionUserId) || string.IsNullOrWhiteSpace(itemId))
                return;

            if (OwnedItemIds.Add(itemId))
            {
                // This is an optimistic purchase delta, not a
                // complete /api/inventory/me snapshot. Preserve
                // IsLoaded so an unloaded session still performs
                // an authoritative inventory refresh later.
                Changed?.Invoke();
            }
        }

        public static void Refresh(
            Action<bool> completed = null)
        {
            EnsureCurrentUser();
            if (string.IsNullOrEmpty(_sessionUserId))
            {
                completed?.Invoke(false);
                return;
            }
            if (IsLoading)
            {
                completed?.Invoke(false);
                return;
            }

            var runtime =
                AuthRuntime.EnsureExists();

            if (runtime == null
                || runtime.Client == null)
            {
                completed?.Invoke(false);
                return;
            }

            IsLoading = true;
            int requestRevision = _revision;
            string requestUserId = _sessionUserId;

            runtime.Client.GetJson<InventoryResponse>(
                ApiEndpoints.InventoryMe,
                true,
                result =>
                {
                    if (requestRevision != _revision
                        || !string.Equals(requestUserId, AuthSession.CurrentUserId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        completed?.Invoke(false);
                        return;
                    }
                    IsLoading = false;

                    if (!result.IsSuccess
                        || result.Data == null
                        || !result.Data.success
                        || result.Data.data == null)
                    {
                        completed?.Invoke(false);
                        return;
                    }

                    OwnedItemIds.Clear();

                    var items =
                        result.Data.data.items;

                    if (items != null)
                    {
                        for (int i = 0; i < items.Length; i++)
                        {
                            var owned =
                                items[i];

                            if (owned == null
                                || string.IsNullOrWhiteSpace(
                                    owned.itemId))
                            {
                                continue;
                            }

                            OwnedItemIds.Add(
                                owned.itemId);
                        }
                    }

                    IsLoaded = true;

                    Changed?.Invoke();
                    completed?.Invoke(true);
                });
        }
    }
}
