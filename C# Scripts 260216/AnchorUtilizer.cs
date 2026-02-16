// Scene 3에서 사용
// Scene Admin에서 박아놓은 앵커들을 불러와서 사용하는 것
// 목적: 앵커와의 거리계산을 통해 사용자 위치 파악

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using TMPro;

public class AnchorUtilizer : MonoBehaviour
{
    [Header("설정")]
    public GameObject anchorPrefab; // Scene_Admin과 동일한 프리팹 사용
    public Transform centerEyeAnchor; // 사용자 헤드셋(CenterEyeAnchor) 연결

    [Header("디버그 UI (TMP)")]
    public TextMeshProUGUI nearestAnchorNameText; // 가장 가까운 앵커 이름 출력용
    public TextMeshProUGUI distanceText;          // 거리 출력용

    [Header("데이터 로드 경로")]
    // Scene_Admin에서 저장한 경로와 동일하게 설정
    private string SavePath => Path.Combine(Application.persistentDataPath, "anchors.json");

    private List<OVRSpatialAnchor> resolvedAnchors = new List<OVRSpatialAnchor>();
    private Dictionary<OVRSpatialAnchor, string> anchorNames = new Dictionary<OVRSpatialAnchor, string>();

    async void Start()
    {
        // 퀘스트 시스템 안정화를 위해 잠시 대기
        await Task.Delay(1000);
        await LoadAndResolveExistingAnchors();
    }

    void Update()
    {
        // 매 프레임 가장 가까운 앵커와의 거리를 계산하여 UI 업데이트
        UpdateNearestAnchorInfo();
    }

    /// <summary>
    /// JSON 파일을 읽어 기존에 생성된 앵커들을 현실 세계에 복원합니다.
    /// </summary>
    async Task LoadAndResolveExistingAnchors()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning($"[Utilizer] 저장된 JSON 파일을 찾을 수 없습니다: {SavePath}");
            return;
        }

        string json = File.ReadAllText(SavePath);
        AnchorListWrapper wrapper = JsonUtility.FromJson<AnchorListWrapper>(json);

        if (wrapper?.anchors == null || wrapper.anchors.Count == 0) return;

        List<Guid> uuidsToLoad = new List<Guid>();
        foreach (var a in wrapper.anchors)
        {
            if (Guid.TryParse(a.uuid, out Guid guid))
                uuidsToLoad.Add(guid);
        }

        var unboundBuffer = new List<OVRSpatialAnchor.UnboundAnchor>();

        // 비동기로 앵커 로드
        await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuidsToLoad, unboundBuffer);

        foreach (var unbound in unboundBuffer)
        {
            bool localized = await unbound.LocalizeAsync();
            if (!localized || !unbound.TryGetPose(out Pose pose)) continue;

            // 앵커 소환 (현재는 디버그를 위해 보임 처리)
            GameObject anchorObj = Instantiate(anchorPrefab, pose.position, pose.rotation);
            OVRSpatialAnchor spatialAnchor = anchorObj.AddComponent<OVRSpatialAnchor>();
            unbound.BindTo(spatialAnchor);

            resolvedAnchors.Add(spatialAnchor);

            // JSON의 placeId 매칭
            var record = wrapper.anchors.Find(a => a.uuid == spatialAnchor.Uuid.ToString());
            if (record != null)
            {
                anchorNames[spatialAnchor] = record.placeId;
                
                // 앵커 머리 위의 텍스트도 업데이트
                var textLabel = anchorObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textLabel != null) textLabel.text = record.placeId;
            }
        }
        Debug.Log($"[Utilizer] 총 {resolvedAnchors.Count}개의 앵커 복원 완료.");
    }

    /// <summary>
    /// 현재 사용자와 가장 가까운 앵커를 찾아 UI에 표시합니다.
    /// </summary>
    private void UpdateNearestAnchorInfo()
    {
        if (resolvedAnchors.Count == 0 || centerEyeAnchor == null) return;

        OVRSpatialAnchor nearestAnchor = null;
        float minDistance = float.MaxValue;

        foreach (var anchor in resolvedAnchors)
        {
            if (anchor == null || !anchor.Localized) continue;

            float dist = Vector3.Distance(centerEyeAnchor.position, anchor.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestAnchor = anchor;
            }
        }

        // UI 업데이트
        if (nearestAnchor != null)
        {
            if (anchorNames.TryGetValue(nearestAnchor, out string placeId))
            {
                nearestAnchorNameText.text = $"가까운 위치: {placeId}";
            }
            distanceText.text = $"거리: {minDistance:F2}m";
        }
    }

    // 데이터 구조 유지를 위한 클래스들
    [Serializable] public class AnchorRecord { public string uuid; public string placeId; }
    [Serializable] public class AnchorListWrapper { public List<AnchorRecord> anchors; }
}
