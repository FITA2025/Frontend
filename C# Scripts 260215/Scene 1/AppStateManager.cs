using UnityEngine;

public class AppStateManager : MonoBehaviour
{
    public static AppStateManager I { get; private set; }

    [Header("Runtime Config")]
    public string serverBaseUrl;
    public int playerId;

    [Header("Defaults")]
    public string defaultServerBaseUrl = "http://43.203.39.23:8000";

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // 런타임 값이 비어있다면 기본값으로 보정
        serverBaseUrl = NormalizeUrlOrDefault(serverBaseUrl);
    }

    public void SetConfig(string url, int id)
    {
        // 사용자가 URL을 비워두면 기본값으로 대체
        serverBaseUrl = NormalizeUrlOrDefault(url);
        playerId = id;
    }

    private string NormalizeUrlOrDefault(string url)
    {
        var trimmed = url?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? defaultServerBaseUrl : trimmed;
    }
}
