using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Android;

public class LaunchFlowController : MonoBehaviour
{
    [Header("UI Refs")]
    public TMP_InputField serverUrlInput;
    public TMP_InputField userIdInput;
    public TextMeshProUGUI errorLabel;
    public TextMeshProUGUI statusLabel;

    [Header("Gate")]
    public GameObject startGateRoot;

    [Header("Scene")]
    public string nextSceneName = "Scene2";
    public string adminScene = "Scene_Admin";

    void Start()
    {
        if (errorLabel != null) errorLabel.text = "";

        // 1) UI는 일단 무조건 열어둠 (권한 때문에 입력/버튼이 잠기지 않게)
        if (startGateRoot != null) startGateRoot.SetActive(true);

        // 2) 서버 URL 기본값 주입
        if (serverUrlInput != null)
        {
            var fallback = AppStateManager.I != null ? AppStateManager.I.defaultServerBaseUrl : "http://43.203.39.23:8000";
            if (string.IsNullOrWhiteSpace(serverUrlInput.text))
                serverUrlInput.text = fallback;
        }

        if (statusLabel != null) statusLabel.text = "Checking Permission...";

        StartCoroutine(CheckAndRequestCameraPermission());
    }

    IEnumerator CheckAndRequestCameraPermission()
    {
        // 이미 있으면 바로 표시만
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            if (statusLabel != null) statusLabel.text = "Camera Permission Comfirmed";
            yield break;
        }

        if (statusLabel != null) statusLabel.text = "Requesting Camera Permission...";
        Permission.RequestUserPermission(Permission.Camera);

        // 권한은 넉넉히 기다리되, 실패해도 UI를 막지 않음
        float timeout = 30f;
        float t0 = Time.time;

        while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            if (Time.time - t0 > timeout)
            {
                if (statusLabel != null)
                    statusLabel.text = "No Camera Permission Yet";
                yield break;
            }
            yield return null;
        }

        if (statusLabel != null) statusLabel.text = "Camera Permission Comfirmed";
    }

    public void OnClickStartDrill()
    {
        // URL: 비어있으면 default로 대체
        var url = serverUrlInput != null ? serverUrlInput.text?.Trim() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = AppStateManager.I != null ? AppStateManager.I.defaultServerBaseUrl : "http://43.203.39.23:8000";
            if (serverUrlInput != null) serverUrlInput.text = url;
        }

        var rawId = userIdInput != null ? userIdInput.text?.Trim() : null;

        if (!IsValidUrl(url))
        {
            if (errorLabel != null) errorLabel.text = "Invalid Form of Server URL(http://x.x.x.x:8000)";
            return;
        }

        if (rawId == "admin")
        {
            SceneManager.LoadScene(adminScene);
        }

        if (!UserIdNormalizer.TryNormalizeToInt(rawId, out var id))
        {
            if (errorLabel != null) errorLabel.text = "Invalid UserID (1 Alphabet + 6 Digits)";
            return;
        }

        if (AppStateManager.I == null)
        {
            if (errorLabel != null) errorLabel.text = "No AppStateManager in Scene1";
            return;
        }

        AppStateManager.I.SetConfig(url, id);
        SceneManager.LoadScene(nextSceneName);
    }

    bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var u) &&
               (u.Scheme == "http" || u.Scheme == "https");
    }
}
