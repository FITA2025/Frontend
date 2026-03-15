// 최신화 260316
// 260316 수정 내용: ServerURLInput -> Floor 입력으로 변경.

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Android;

public class LaunchFlowController : MonoBehaviour
{
    [Header("UI Refs")]
    public TMP_InputField currFloorInput;
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

        // UI는 일단 무조건 열어둠 (권한 때문에 입력/버튼이 잠기지 않게)
        if (startGateRoot != null) startGateRoot.SetActive(true);
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
        // 서버 접속 URL
        var url = AppStateManager.I.defaultServerBaseUrl;

        var rawId = userIdInput != null ? userIdInput.text?.Trim() : null;

        if (rawId == "admin")
        {
            SceneManager.LoadScene(adminScene);
        }

        if(!currFloorInput)
        {
            errorLabel.text = "Floor should be a number between 1-10.";
            return;
        }

        if (!UserIdNormalizer.TryNormalizeToInt(rawId, out var id))
        {
            if (errorLabel != null) errorLabel.text = "UserID should be 1 Alphabet + 6 Digits.";
            return;
        }

        if (AppStateManager.I == null)
        {
            if (errorLabel != null) errorLabel.text = "No AppStateManager in Scene1";
            return;
        }

        // 입력된 층 문자열을 int로 변환하여 저장
        if (int.TryParse(currFloorInput.text, out int floor))
        {
            AppStateManager.I.initFloor = floor; // 전역 상태에 층 정보 저장
        }

        AppStateManager.I.SetConfig(url, id);
        SceneManager.LoadScene(nextSceneName);
    }
}
