using System;
using UnityEngine;
using UnityEngine.Purchasing;

namespace MazeRunner
{
    /// <summary>
    /// Google Play billing for the single non-consumable "unlock_full" (same product ID as v0.2,
    /// so existing buyers keep their purchase and the Play Console product needs no changes).
    /// </summary>
    public sealed class Store : MonoBehaviour, IStoreListener
    {
        public const string ProductId = "unlock_full";
        public static Store I { get; private set; }

        public static event Action Unlocked;
        public static event Action<string> Failed;

        IStoreController _controller;
        IExtensionProvider _extensions;

        public bool Ready => _controller != null;

        public string Price
        {
            get
            {
                var p = _controller?.products.WithID(ProductId);
                return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : "";
            }
        }

        public static Store Create(Transform parent)
        {
            var go = new GameObject("Store");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Store>();
        }

        void Awake()
        {
            I = this;
            try
            {
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                builder.AddProduct(ProductId, ProductType.NonConsumable);
                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception e) { Debug.LogWarning("[Store] init threw: " + e.Message); }
        }

        public void Buy()
        {
            if (Save.FullUnlocked) { Unlocked?.Invoke(); return; }
            if (_controller == null) { Failed?.Invoke(Loc.Get("store_wait")); return; }
            _controller.InitiatePurchase(ProductId);
        }

        /// <summary>Google Play restores owned items on init; re-checking the receipt is all that is needed.</summary>
        public void Restore()
        {
            if (_controller == null) { Failed?.Invoke(Loc.Get("store_wait")); return; }
            var p = _controller.products.WithID(ProductId);
            if (p != null && p.hasReceipt) Grant();
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            var p = controller.products.WithID(ProductId);
            if (p != null && p.hasReceipt) Grant();
        }

        public void OnInitializeFailed(InitializationFailureReason error) => Debug.LogWarning($"[Store] init failed: {error}");
        public void OnInitializeFailed(InitializationFailureReason error, string message) => Debug.LogWarning($"[Store] init failed: {error} {message}");

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == ProductId) Grant();
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            Debug.LogWarning($"[Store] purchase failed: {reason}");
            if (reason != PurchaseFailureReason.UserCancelled) Failed?.Invoke(reason.ToString());
        }

        void Grant()
        {
            bool was = Save.FullUnlocked;
            Save.FullUnlocked = true;
            if (!was) Unlocked?.Invoke();
        }
    }
}
