using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using System.Dynamic;

public class AnchorManager : MonoBehaviour
{
    [Header("설정")]
    public GameObject anchorPrefab; // 앵커 위치를 시각화할 프리팹 (반투명 큐브 등)
    public Transform controllerTransform; // 오른쪽 컨트롤러(RTouch)의 Transform 연결

    [Header("데이터 관리")]
    private List<OVRSpatialAnchor> createdAnchors = new List<OVRSpatialAnchor>();

    [Header("json 저장 경로")]
    private string SavePath => Path.Combine(Application.persistentDataPath, "anchors.json");


    [Header("UI & Keyboard")]
    public Transform centerEyeAnchor; // 사용자의 시선 Camera
    public GameObject inputCanvas;         // PlaceId 입력을 위한 WorldSpace Canvas
    public TMP_InputField placeIdInputField; // 가상 키보드와 연결될 입력창
    public OVRVirtualKeyboard virtualKeyboard; // 제공해주신 스크립트가 붙은 오브젝트
    private OVRSpatialAnchor pendingAnchor;     // 현재 이름을 입력받고 있는 대상 앵커
    private Dictionary<OVRSpatialAnchor, string> anchorNames = new Dictionary<OVRSpatialAnchor, string>();  // 앵커와 메타데이터를 매칭하기 위한 딕셔너리

    // 기본 메타데이터 (이 값들은 나중에 별도 설정 UI나 JSON 편집으로 관리)
    public int currentFloor = 1;
    public string currentRoomId = "T501_1";
    public string currentType = "gate"; // gate, scenario, alignment


    // 클래스 멤버 변수
    private bool isUIOpening = false; // UI가 막 열리고 있는 중인지 체크


    // 앱 시작 시 저장된 앵커들을 불러옴
    async void Start()
    {
        // 퀘스트 시스템이 준비될 때까지 잠시 대기
        await Task.Delay(1000);
        await LoadAndResolveAnchorsAsync();
    }

    void Update()
    {
        // 앵커 생성: 검지 트리거
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            // 비동기 함수 호출 (Fire and forget)
            _ = CreateAnchorAsync();
        }

        // 앵커 삭제 (중지 그립)
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            _ = DeleteNearbyAnchorAsync();
        }

        // json에 앵커 데이터 저장: B 버튼
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            SaveAllAnchorsToJson();
            Vibrate(0.5f); // 진동 피드백
        }
    }


    /// <summary>
    /// 컨트롤러 위치에 Spatial Anchor를 생성하고 시스템에 등록(비동기 생성 로직)
    /// </summary>
    async Task CreateAnchorAsync()
    {
        // 위치는 컨트롤러 끝점, 회전은 수직(Y축)만 유지.
        Vector3 pos = controllerTransform.position;
        Quaternion rot = Quaternion.Euler(0, controllerTransform.eulerAngles.y, 0);

        // 시각화용 큐브
        GameObject anchorObj = Instantiate(anchorPrefab, pos, rot);

        // Meta Spatial Anchor 컴포넌트 추가 및 생성 시작
        OVRSpatialAnchor spatialAnchor = anchorObj.AddComponent<OVRSpatialAnchor>();

        // 앵커 구성이 완료될 때까지 대기
        while (!spatialAnchor.Created) await Task.Yield();

        // 앵커를 Quest 디바이스에 영구 저장(Persist)
        bool isSuccess = await spatialAnchor.SaveAnchorAsync();

        if (isSuccess)
        {
            createdAnchors.Add(spatialAnchor);
            pendingAnchor = spatialAnchor;
            Debug.Log($"[Anchor] 저장 성공: {spatialAnchor.Uuid}");

            // 1. [추가] 검지 트리거에서 손을 뗄 때까지 대기 (중요!)
            while (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                await Task.Yield();
            }

            // 2. 입력 UI 호출
            ShowInputUI(pos);

            // 진동 피드백
            OVRInput.SetControllerVibration(0.5f, 0.5f, OVRInput.Controller.RTouch);
            await Task.Delay(100); // 0.1초 대기
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }
        else
        {
            Debug.LogError("[Anchor] 저장 실패");
            Destroy(anchorObj);
        }
    }

    void ShowInputUI(Vector3 anchorPosition)
    {
        isUIOpening = true;
        
        // 사용자 시선 카메라 설정. NULL 시 자동으로 찾음
        Transform camTransform = centerEyeAnchor;
        if(camTransform == null) camTransform = Camera.main.transform;

        // 입력창 위치 설정
        inputCanvas.transform.position = anchorPosition + Vector3.up * 0.4f;
        inputCanvas.transform.LookAt(camTransform);
        inputCanvas.transform.Rotate(0, 180, 0);
        inputCanvas.SetActive(true);

        // [중요] 키보드 활성화 및 위치 소환
        if (virtualKeyboard != null)
        {
            virtualKeyboard.gameObject.SetActive(true);

            // OVRVirtualKeyboard 위치 설정
            // 사용자 시선의 수평 방향으로 키보드 배치
            Vector3 forward = camTransform.forward;
            forward.y = 0; // 수직 성분 제거
            forward.Normalize();

            // 사용자 정면 0.4m 앞, 카메라 높이로부터 0.5m 아래에 배치
            Vector3 targetPos = camTransform.position + (forward * 0.4f) + (Vector3.up * -0.5f);
            virtualKeyboard.transform.position = targetPos;
            
            // 키보드가 사용자를 정면으로 바라보게 설정, 65도 눕힘
            virtualKeyboard.transform.LookAt(new Vector3(camTransform.position.x, targetPos.y, camTransform.position.z));
            virtualKeyboard.transform.Rotate(65f, 180f, 0f);

            // 키보드 Resize (Meta에서 공식 사용 중인 0.4배를 동일하게 적용)
            virtualKeyboard.transform.localScale = Vector3.one * 0.4f;
        }

        placeIdInputField.text = "";
        // placeIdInputField.Select(); // 입력 필드 포커스 (정상작동 시 삭제할 것)
        placeIdInputField.ActivateInputField();

        Invoke(nameof(ResetUIOpeningFlag), 0.5f);
    }

    void ResetUIOpeningFlag() => isUIOpening = false;



    // 키보드 Enter 이벤트에 연결할 함수
    public void OnInputComplete()
    {
        if (isUIOpening) return;

        if (pendingAnchor != null)
        {
            string finalName = placeIdInputField.text;
            if (string.IsNullOrEmpty(finalName)) finalName = "Unnamed";

            // 딕셔너리에 PlaceId 저장
            anchorNames[pendingAnchor] = finalName;
            
            // 큐브 자식에 있는 TextMeshPro를 찾아 텍스트 업데이트
            var textLabel = pendingAnchor.GetComponentInChildren<TextMeshProUGUI>();
            if (textLabel != null) textLabel.text = finalName;
            SaveAllAnchorsToJson();

            Debug.Log($"[Anchor] 이름 부여 완료: {finalName}");
        }

        // UI 닫기
        inputCanvas.SetActive(false);
        virtualKeyboard.gameObject.SetActive(false);
        pendingAnchor = null;
    }


    // 저장된 json을 읽어서 앵커를 다시 현실에 배치
    // 최대 50개 UUID만 처리할 수 있음
    async Task LoadAndResolveAnchorsAsync()
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        AnchorListWrapper wrapper = JsonUtility.FromJson<AnchorListWrapper>(json);

        if (wrapper?.anchors == null || wrapper.anchors.Count == 0) return;

        // 1) UUID 리스트 생성
        List<Guid> uuidsToLoad = new List<Guid>();
        foreach (var a in wrapper.anchors)
        {
            if (Guid.TryParse(a.uuid, out Guid guid) && guid != Guid.Empty)
                uuidsToLoad.Add(guid);
        }

        if (uuidsToLoad.Count == 0) return;

        // 2) v81 권장: (uuids, 결과버퍼, incremental callback)
        // NOTE: 내부적으로 "처음 50개 UUID만" 처리함. (필요하면 50개씩 끊어서 호출)  :contentReference[oaicite:1]{index=1}
        var unboundBuffer = new List<OVRSpatialAnchor.UnboundAnchor>();

        // 반환값(OVRResult)은 받아도 되고, 일단 await만 해도 unboundBuffer가 채워짐
        await OVRSpatialAnchor.LoadUnboundAnchorsAsync(
            uuidsToLoad,
            unboundBuffer,
            null // incremental callback 필요하면 여기에 넣기
        );

        if (unboundBuffer.Count == 0) return;

        // 3) Localize -> Pose -> Instantiate -> BindTo
        foreach (var unbound in unboundBuffer)
        {
            bool localized = await unbound.LocalizeAsync(0); // 0 = no timeout
            if (!localized)
            {
                Debug.LogWarning($"[Anchor] Localize 실패: {unbound.Uuid}");
                continue;
            }

            if (!unbound.TryGetPose(out Pose pose))
            {
                Debug.LogWarning($"[Anchor] Pose 획득 실패: {unbound.Uuid}");
                continue;
            }

            GameObject anchorObj = Instantiate(anchorPrefab, pose.position, pose.rotation);

            // 런타임에 Prefab을 Anchor화 시킴. (OVRSpatialAnchor 컴포넌트를 주입)
            OVRSpatialAnchor spatialAnchor = anchorObj.AddComponent<OVRSpatialAnchor>();

            unbound.BindTo(spatialAnchor);

            createdAnchors.Add(spatialAnchor);
            Debug.Log($"[Anchor] 복원 완료: {spatialAnchor.Uuid}");

            // [추가] JSON 데이터에서 이 앵커의 uuid에 해당하는 placeId 찾기
            var record = wrapper.anchors.Find(a => a.uuid == spatialAnchor.Uuid.ToString());
            if (record != null)
            {
                anchorNames[spatialAnchor] = record.placeId; // 딕셔너리 복구
                var textLabel = anchorObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textLabel != null) textLabel.text = record.placeId; // 텍스트 표시
            }
        }
    }



    // 진동 편의 함수
    private async void Vibrate(float amplitude)
    {
        OVRInput.SetControllerVibration(amplitude, amplitude, OVRInput.Controller.RTouch);
        await Task.Delay(100);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }


    /// <summary>
    /// 최신 EraseAsync를 사용하는 비동기 삭제 로직
    /// </summary>
    async Task DeleteNearbyAnchorAsync()
    {
        if (createdAnchors.Count == 0) return;
        bool deleted = false;
        
        // 뒤에서부터 검색해야 삭제 시 에러가 나지 않습니다.
        for (int i = createdAnchors.Count - 1; i >= 0; i--)
        {
            var anchor = createdAnchors[i];
            if (anchor == null) continue;

            float distance = Vector3.Distance(controllerTransform.position, anchor.transform.position);

            // 거리 기준을 0.3m로 살짝 넉넉하게 잡았습니다.
            if (distance <= 0.3f)
            {
                Debug.Log($"[Anchor] 삭제 대상 발견: {anchor.Uuid}");
                var result = await anchor.EraseAnchorAsync();
                
                if (result.Success)
                {
                    createdAnchors.RemoveAt(i); // 리스트에서 제거
                    Destroy(anchor.gameObject); // 씬에서 제거
                    deleted = true;
                    Debug.Log("[Anchor] 삭제 성공");
                }
                break; // 하나 지웠으면 루프 종료
            }
        }

        if (deleted)
        {
            // 삭제 즉시 JSON 업데이트
            SaveAllAnchorsToJson();
            Vibrate(0.3f);
        }
    }


    // 현재까지 생성된 모든 앵커의 UUID와 메타데이터를 json으로 저장.
    // UI의 'Save' 버튼에 이 함수를 연결해야 함.
    // JSON 저장 로직은 이전과 동일 (SaveAllAnchorsToJson 함수 유지)
    // 저장경로: Android\data\com.UnityTechnologies.com.unity.template.urpblank\files\anchors.json
    public void SaveAllAnchorsToJson()
    {
        List<AnchorRecord> anchorMetadataList = new List<AnchorRecord>();

        foreach (var anchor in createdAnchors)
        {
            // 딕셔너리에 이름이 있으면 가져오고 없으면 기본값 사용
        if (!anchorNames.TryGetValue(anchor, out string resolvedPlaceId))
                {
                    resolvedPlaceId = "Unnamed_Point";
                }

            anchorMetadataList.Add(new AnchorRecord {
                uuid = anchor.Uuid.ToString(),      // 32자리 UUID 추출
                floor = currentFloor,
                roomId = currentRoomId,
                anchorType = currentType,
                placeId = resolvedPlaceId,
                pairGroupId = "" // 필요 시에만 입력 (NULL 허용)
            });
        }

        string json = JsonUtility.ToJson(new AnchorListWrapper { anchors = anchorMetadataList }, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[JSON] 저장 완료: {SavePath}");
    }

    
    // JSON 파싱용 클래스들 (스크립트 하단 혹은 별도 파일)
    [Serializable] public class AnchorRecord { public string uuid; public int floor; public string roomId; public string anchorType; public string placeId; public string pairGroupId; }
    [Serializable] public class AnchorListWrapper { public List<AnchorRecord> anchors; }
}
