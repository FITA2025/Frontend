using System;

[Serializable]
public class AnchorRecord
{
    public string uuid;          // Meta Spatial Anchor의 고유 ID (NOT NULL, 메타퀘스트에서 자동으로 등록하는 것이라 임의 수정 불가)
    public int floor;           // 층 정보 (1-10) (NOT NULL)
    public string roomId;       // 호실 정보 (예: T503, 5층복도, ...) (NOT NULL)
    public string anchorType;   // 종류: Gate, Scenario, Alignment
    public string placeId;      // 관리자가 직접 등록하는 ID (예: T501_1, T501_2, T5_Toilet, ...) (NOT NULL) 
    public string pairGroupId;  // Anchor위치 재정렬을 위한 Alignment Anchor 세트 구분을 위한 ID (외래키) (지금은 신경쓰지 X)
}

[Serializable]
public class AnchorListWrapper
{
    public System.Collections.Generic.List<AnchorRecord> anchors;
}
