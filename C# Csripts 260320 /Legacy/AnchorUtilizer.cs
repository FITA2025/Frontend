// 최신화 260315

// 스크립트 이름 : AnchorUtilizer.cs
// 스크립트 기능 : 1. AppStateManager에서 사용자가 입력한 초기 층(currFloor) 값을 가져옴.
//                 2. 해당 층에 해당하는 Spatial Anchor를 JSON 파일에서 읽어와 50개씩 복원.
//                 3. 매 프레임 사용자와 가장 가까운 앵커를 탐색하여 실시간 위치를 업데이트합니다.
//                 4. 특정 계단 구역 앵커(stairEnterNum 1, 2)에 접근하면 인접 층의 데이터를 Preload.
//                 5. 새로운 층 이동 후 위치 안정화 시(STABLE_THRESHOLD) 현재 층 외의 다른 층 앵커들을 Unload.
// 입력 파라미터 : AppStateManager.I.initFloor(AppStateManager.cs), anchors.json
// 리턴 타입 : 없음 (MonoBehaviour)

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using System.Security.Cryptography.X509Certificates;

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

    
    [Header("Anchor 로드 설정")]
    // 메모리 관리 데이터 (Load된 Anchor Set 관리)
    private List<OVRSpatialAnchor> resolvedAnchors = new List<OVRSpatialAnchor>();
    private Dictionary<OVRSpatialAnchor, AnchorRecord> anchorMetadata = new Dictionary<OVRSpatialAnchor, AnchorRecord>();
    private HashSet<int> currentlyLoadedFloors = new HashSet<int>();

    [Header("층 관리 설정")]
    private int currentFloor = -1; // 현재 로드된 층 추적 (Init: -1)
    private OVRSpatialAnchor lastNearestAnchor = null; // 마지막으로 인식된 가장 가까운 앵커
    private int stableFloorCount = 0; // 앵커 변경 횟수를 세는 안정화 카운터
    private const int STABLE_THRESHOLD = 3; // n회 연속 동일 층 인식 시 그외 층의 Anchor를 언로드
    public int stairEnterNum_1 = 54; // 메인 계단 입구 AnchorNUM
    public int stairEnterNum_2 = 67; // 사이드 계단 입구 AnchorNUM


    // 함수 이름 : Start()
    // 함수 기능 : 스크립트 활성화 시 최초 1회 실행.
    //             1초 대기 후 사용자가 입력한 floor를 Load.
    // 입력 파라미터 : 없음
    // 리턴 타입 : void (async)
    async void Start()
    {
        // 퀘스트 시스템 안정화를 위해 잠시 대기
        await Task.Delay(1000);
        InitializeFirstFloorLoad();
    }

    // 함수 이름 : Update()
    // 함수 기능 : 유니티 엔진에 의해 매 프레임 호출됨.
    //             사용자와 가장 가까운 앵커를 탐색 -> 층 이동 로직을 trigger.
    // 입력 파라미터 : 없음
    // 리턴 타입 : void
    void Update()
    {
        // 매 프레임 가장 가까운 앵커와의 거리를 계산하여 UI 업데이트
        UpdateNearestAnchorInfo();
    }



    // 함수 이름 : InitializeFirstFloorLoad()
    // 함수 기능 : Anchor Load 진입점.
    //             앱 시작 시 LaunchFlowController에서 설정되어 AppStateManager에 저장된 현재 층 정보를 가져옴.
    //             현재 floor를 바탕으로 최초 Anchor들의 Load를 수행.
    // 입력 파라미터 : 없음
    // 리턴 타입 : void
    public void InitializeFirstFloorLoad()
    {
        int startFloor = 1; // 기본값

        // LaunchFlowController가 저장한 값을 AppStateManager 싱글톤에서 참조
        if (AppStateManager.I != null)
        {
            startFloor = AppStateManager.I.initFloor;
            Debug.Log($"[Utilizer] Got <floor {startFloor}> from AppStateManager of Scene 1.");
        }
        else
        {
            Debug.LogWarning("[Utilizer] Cannot get floor value from AppStateManager.");
            Debug.LogWarning("[Utilizer] Starting from floor 1 (Default Setting).");
        }

        // 초기 층 로드 (isPreloadNearby는 false로 시작)
        _ = ChangeFloorSystem(startFloor, false);
    }



    
    // 함수 이름 : ChangeFloorSystem()
    // 함수 기능 : 층 전환에 따라 load되는 Anchor 관리 로직.
    //             A. 특정 층에 대한 중복 로드 방지
    //             B. 계단에 있는 Anchor 진입 시 인접 층(위아래 +- 1층)에 대한 앵커 로드.
    // 입력 파라미터 : int targetFloor (대상 층), bool isPreloadNearby (인접 층 로드 여부)
    // 리턴 타입 : Task
    private async Task ChangeFloorSystem(int targetFloor, bool isPreloadNearby)
    {
        // 이미 해당 층이 로드되어 있다면 중복 로드 방지
        if (currentlyLoadedFloors.Contains(targetFloor) && !isPreloadNearby) return;

        // 인접 층 사전 로드 (Pre-loading) 로직 포함
        List<int> floorsToLoad = new List<int> { targetFloor };

        // 현재 층과 위아래 1개 층을 load 대상인 targetFloor 리스트에 포함
        if (isPreloadNearby)
        {
            if (targetFloor + 1 <= 10) floorsToLoad.Add(targetFloor + 1);
            if (targetFloor - 1 >= 1) floorsToLoad.Add(targetFloor - 1);
        }

        // targetFloor에 있는 층의 Anchor들을 load.
        foreach (int f in floorsToLoad)
        {
            if (currentlyLoadedFloors.Contains(f)) continue;
            await LoadAnchorsByFloor(f); // 함수 호출
        }
    }



    // 함수 이름 : LoadAnchorsByFloor()
    // 함수 기능 : 1. JSON 데이터에서 특정 층에 해당하는 앵커 UUID 리스트를 추출.
    //             2. 50개 단위(Batch)로 기기에서 앵커를 복원.
    // 입력 파라미터 : int floorToLoad (로드할 층 번호)
    // 리턴 타입 : Task
    async Task LoadAnchorsByFloor(int floorToLoad)
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        AnchorListWrapper wrapper = JsonUtility.FromJson<AnchorListWrapper>(json);
        if (wrapper?.anchors == null) return;

        List<Guid> floorUuids = new List<Guid>();
        List<AnchorRecord> filteredRecords = new List<AnchorRecord>();

        foreach (var a in wrapper.anchors)
        {
            if (a.floor == floorToLoad && Guid.TryParse(a.uuid, out Guid guid))
            {
                floorUuids.Add(guid);
                filteredRecords.Add(a);
            }
        }

        // Anchor들을 50개씩 Load (OS 레벨 제한으로 인한 Batch화)
        const int BatchSize = 50;
        for (int i = 0; i < floorUuids.Count; i += BatchSize)
        {
            int count = Math.Min(BatchSize, floorUuids.Count - i);
            var batch = floorUuids.GetRange(i, count);
            var unboundBuffer = new List<OVRSpatialAnchor.UnboundAnchor>();

            await OVRSpatialAnchor.LoadUnboundAnchorsAsync(batch, unboundBuffer);

            foreach (var unbound in unboundBuffer)
            {
                if (await unbound.LocalizeAsync() && unbound.TryGetPose(out Pose pose))
                {
                    // Anchor Prefab을 생성
                    GameObject obj = Instantiate(anchorPrefab, pose.position, pose.rotation);
                    OVRSpatialAnchor sa = obj.AddComponent<OVRSpatialAnchor>();
                    unbound.BindTo(sa);
                    resolvedAnchors.Add(sa);

                    // 메타데이터 매칭 확인
                    var record = filteredRecords.Find(r => r.uuid == sa.Uuid.ToString());
                    if (record != null)
                    {
                        anchorMetadata[sa] = record; 

                        // prefab의 TMP에 anchorNUM 쓰기
                        TextMeshPro textComp = obj.GetComponentInChildren<TextMeshPro>();
                        if (textComp != null)
                        {
                            textComp.text = record.anchorNUM.ToString(); // 앵커 번호 표시
                        }
                        else
                        {
                            Debug.LogWarning($"[Utilizer] TextMeshPro component not found in prefab for anchor {record.anchorNUM}");
                        }
                    }
                }
            }
        }
        
        currentlyLoadedFloors.Add(floorToLoad); // [추가] 로드된 층 목록에 추가
        Debug.Log($"[Utilizer] Done loading anchors of floor {floorToLoad}.");
    }



    // 함수 이름 : UpdateNearestAnchorInfo()
    // 함수 기능 : 1. 사용자 실내 위치 추적
    //             2. Stable Count 사용 -> 현재 층에 머물고 있는지, 계속 층을 옮기는지 판단.
    // 입력 파라미터 : 없음
    // 리턴 타입 : void
    
    // 층 이동 전 사전 감지 및 Anchor load할 floor 개수 관리
    private void UpdateNearestAnchorInfo()
    {
        if (resolvedAnchors.Count == 0 || centerEyeAnchor == null) return;

        OVRSpatialAnchor nearestAnchor = null;
        float minDistance = float.MaxValue;

        // 1. 가장 가까운 Anchor 찾기
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

        // 2. UI 업데이트 및 층 이동 판정
        if (nearestAnchor != null && anchorMetadata.TryGetValue(nearestAnchor, out AnchorRecord record))
        {
            nearestAnchorNameText.text = $"Nearest: {record.anchorNUM} ({record.floor}F)";
            distanceText.text = $"Distance: {minDistance:F2}m";

            // Nearest Anchor가 변경되었을 경우를 감지
            if (nearestAnchor != lastNearestAnchor)
            {
                lastNearestAnchor = nearestAnchor; // 최신 Nearest Anchor 갱신

                // 층 자체가 바뀌었는지 확인
                if (record.floor != currentFloor)
                {
                    currentFloor = record.floor;
                    stableFloorCount = 0; // 층이 바뀌면 카운트 리셋
                    Debug.Log($"[Utilizer] Floor Changed to {currentFloor}. StableFloorCount Reset.");
                }
                else
                {
                    // 같은 층 내에서 앵커만 바뀐 경우 -> 안정화 카운트 증가
                    stableFloorCount++;

                    if (stableFloorCount >= STABLE_THRESHOLD && currentlyLoadedFloors.Count > 1)
                    {
                        UnloadOtherFloors(currentFloor);
                    }
                }
            }

            // stairEnterNum 변수: 계단 입구에 왔음을 감지 -> 인접 층의 Anchor를 미리 Load.
            if (minDistance < 1.0f && (record.anchorNUM == stairEnterNum_1 || record.anchorNUM == stairEnterNum_2))
            {
                _ = ChangeFloorSystem(currentFloor, true);
            }
        }
    }

    // 함수 이름 : UnloadOtherFloors()
    // 함수 기능 : 1. 현재 머물고 있는 층을 제외한 나머지 모든 층의 앵커 오브젝트를 파괴.
    //             2. 메모리 및 관리 딕셔너리에서 제거(메모리 최적화).
    // 입력 파라미터 : int keepFloor (유지할 현재 층 번호)
    // 리턴 타입 : void
    private void UnloadOtherFloors(int keepFloor)
    {   
        for (int i = resolvedAnchors.Count - 1; i >= 0; i--)
        {
            var anchor = resolvedAnchors[i];
            if (anchorMetadata.TryGetValue(anchor, out AnchorRecord rec))
            {
                if (rec.floor != keepFloor)
                {
                    anchorMetadata.Remove(anchor);
                    resolvedAnchors.RemoveAt(i);
                    Destroy(anchor.gameObject);
                }
            }
        }

        currentlyLoadedFloors.Clear();
        currentlyLoadedFloors.Add(keepFloor);
        stableFloorCount = 0; // 언로드 완료 후 리셋
        Debug.Log($"[Utilizer] Unloaded all anchors except floor {keepFloor}.");
    }
}
