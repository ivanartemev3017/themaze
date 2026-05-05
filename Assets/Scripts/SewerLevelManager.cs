using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SewerLevelManager : MonoBehaviour
{
    public static SewerLevelManager Instance { get; private set; }

    [Header("Settings")]
    public float levelTime = 200f;

    float _timeLeft;
    bool  _over;

    void Awake()
    {
        Instance = this;
        _timeLeft = levelTime;
        DOTween.Init();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (_over) return;
        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f) Lose("Время вышло");
    }

    public float TimeLeft => _timeLeft;

    public void PlayerReachedExit()
    {
        if (_over) return;
        _over = true;
        Debug.Log("[SewerLevel] Escaped!");
        DOVirtual.DelayedCall(1.5f, () => SceneManager.LoadScene("MainMenu"));
    }

    public void PlayerCaught()
    {
        if (_over) return;
        Lose("Пойман");
    }

    void Lose(string reason)
    {
        _over = true;
        Debug.Log("[SewerLevel] Lose: " + reason);
        DOVirtual.DelayedCall(2f, () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
    }
}
