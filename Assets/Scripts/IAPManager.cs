using UnityEngine;
using UnityEngine.Purchasing;
using System;

public class IAPManager : MonoBehaviour, IStoreListener
{
    public static IAPManager Instance { get; private set; }
    public static event Action OnUnlocked;

    private const string ProductId = "unlock_full";
    private const string PrefKey   = "FullUnlocked";

    private IStoreController   _store;
    private IExtensionProvider _extensions;

    public static bool IsUnlocked => PlayerPrefs.GetInt(PrefKey, 0) == 1;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitIAP();
    }

    private void InitIAP()
    {
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(ProductId, ProductType.NonConsumable);
        UnityPurchasing.Initialize(this, builder);
    }

    public void Purchase()
    {
        if (IsUnlocked) { OnUnlocked?.Invoke(); return; }
        if (_store == null) { Debug.LogWarning("IAP store not ready"); return; }
        _store.InitiatePurchase(ProductId);
    }

    // ── IStoreListener ────────────────────────────────────────────────────────

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _store      = controller;
        _extensions = extensions;
        // Non-consumables are restored automatically on Google Play re-init
        var product = controller.products.WithID(ProductId);
        if (product != null && product.hasReceipt)
            GrantUnlock();
    }

    public void OnInitializeFailed(InitializationFailureReason error) =>
        Debug.LogWarning($"IAP init failed: {error}");

    public void OnInitializeFailed(InitializationFailureReason error, string message) =>
        Debug.LogWarning($"IAP init failed: {error} — {message}");

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (args.purchasedProduct.definition.id == ProductId)
            GrantUnlock();
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason) =>
        Debug.LogWarning($"Purchase failed: {failureReason}");

    // ─────────────────────────────────────────────────────────────────────────

    private void GrantUnlock()
    {
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();
        OnUnlocked?.Invoke();
    }
}
