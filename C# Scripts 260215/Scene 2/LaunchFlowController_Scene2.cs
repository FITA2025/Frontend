using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LaunchFlowController_Scene2 : MonoBehaviour
{
    [Header("Scene")]
    public string nextSceneName = "Scene3";

    [Header("Head Tracking")]
    public Transform headTransform;
    public float requiredAngleEachSide = 60f;

    [Header("Gauge UI")]
    public Image radialGaugeFill;
    public Transform gaugeRoot;
    public bool followHead = true;
    public float gaugeDistance = 2.0f;
    public float gaugeVerticalOffset = -0.1f;

    [Header("Instruction Text")]
    public TMP_Text instructionLabel;
    [Tooltip("둘 다 미완료일 때")]
    public string msgNeedBoth = "Quickly check left and right.";
    [Tooltip("왼쪽 완료 / 오른쪽 미완료일 때")]
    public string msgNeedRight = "Make sure you've checked your right side!";
    [Tooltip("오른쪽 완료 / 왼쪽 미완료일 때")]
    public string msgNeedLeft = "Make sure you've checked your left side!";
    [Tooltip("둘 다 완료(블랙아웃 직전)")]
    public string msgDone = "It's a fire! Mov to evacuate.";

    [Tooltip("MsgDone을 화면에 유지하는 시간(초)")]
    public float msgDoneDisplaySeconds = 2.0f;

    [Header("Blackout (Object toggle)")]
    public GameObject blackoutObject;

    [Tooltip("블랙아웃 된 뒤 Scene 로드까지 대기(초)")]
    public float blackoutHoldSeconds = 2.0f;

    float _initialYaw;
    float _minDeltaYaw;
    float _maxDeltaYaw;
    bool _completed;

    void Awake()
    {
        if (headTransform == null && Camera.main != null)
            headTransform = Camera.main.transform;

        if (radialGaugeFill != null) radialGaugeFill.fillAmount = 0f;

        if (blackoutObject != null)
            blackoutObject.SetActive(false);

        SetInstruction(msgNeedBoth);
    }

    IEnumerator Start()
    {
        yield return null;

        if (headTransform == null)
        {
            Debug.LogError("[LaunchFlowController_Scene2] headTransform is null.");
            enabled = false;
            yield break;
        }

        _initialYaw = headTransform.eulerAngles.y;
        _minDeltaYaw = 0f;
        _maxDeltaYaw = 0f;
        _completed = false;
    }

    void Update()
    {
        if (_completed) return;
        if (headTransform == null) return;

        if (followHead && gaugeRoot != null)
        {
            Vector3 forward = headTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 targetPos = headTransform.position
                                + forward * gaugeDistance
                                + Vector3.up * gaugeVerticalOffset;

            gaugeRoot.position = targetPos;

            Vector3 lookDir = gaugeRoot.position - headTransform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 1e-6f)
                gaugeRoot.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        float currentYaw = headTransform.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(_initialYaw, currentYaw);

        if (deltaYaw < _minDeltaYaw) _minDeltaYaw = deltaYaw;
        if (deltaYaw > _maxDeltaYaw) _maxDeltaYaw = deltaYaw;

        float leftProgress = Mathf.Clamp01((-_minDeltaYaw) / requiredAngleEachSide);
        float rightProgress = Mathf.Clamp01((_maxDeltaYaw) / requiredAngleEachSide);

        bool leftDone = leftProgress >= 1f;
        bool rightDone = rightProgress >= 1f;

        float fill = Mathf.Clamp01((leftProgress + rightProgress) * 0.5f);

        if (radialGaugeFill != null)
            radialGaugeFill.fillAmount = fill;

        if (!leftDone && !rightDone)
        {
            SetInstruction(msgNeedBoth);
        }
        else if (leftDone && !rightDone)
        {
            SetInstruction(msgNeedRight);
        }
        else if (!leftDone && rightDone)
        {
            SetInstruction(msgNeedLeft);
        }
        else
        {
            // 완료: MsgDone 보여주고, 2초 뒤 블랙아웃 → 2초 뒤 씬 로드
            _completed = true;
            SetInstruction(msgDone);
            StartCoroutine(DoneFlow());
        }
    }

    IEnumerator DoneFlow()
    {
        // 1) MsgDone 유지 시간
        if (msgDoneDisplaySeconds > 0f)
            yield return new WaitForSeconds(msgDoneDisplaySeconds);

        // 2) 블랙아웃
        if (blackoutObject != null)
            blackoutObject.SetActive(true);

        // 3) 블랙아웃 유지 후 씬 전환
        if (blackoutHoldSeconds > 0f)
            yield return new WaitForSeconds(blackoutHoldSeconds);

        SceneManager.LoadScene(nextSceneName);
    }

    void SetInstruction(string msg)
    {
        if (instructionLabel == null) return;
        if (instructionLabel.text == msg) return;
        instructionLabel.text = msg;
    }
}
