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

        public static bool IsLoaded { get; private set; }
        public static bool IsLoading { get; private set; }

        public static void Clear()
        {
            OwnedItemIds.Clear();
            IsLoaded = false;
            IsLoading = false;
            Changed?.Invoke();
        }

        public static bool OwnsItem(
            string itemId)
        {
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
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            if (OwnedItemIds.Add(itemId))
            {
                IsLoaded = true;
                Changed?.Invoke();
            }
        }

        public static void Refresh(
            Action<bool> completed = null)
        {
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

            runtime.Client.GetJson<InventoryResponse>(
                ApiEndpoints.InventoryMe,
                true,
                result =>
                {
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
