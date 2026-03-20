// 260316 최신화
// 최신화 내용: anchorNUM 타입 string -> int로 변경.

using System;

[Serializable]
public class AnchorRecord
{
    public string uuid;          // Meta Spatial Anchor의 고유 ID (NOT NULL, 메타퀘스트에서 자동으로 등록하는 것이라 임의 수정 불가)
    public int floor;           // 층 정보 (1-10) (NOT NULL)
    public string roomId;       // 호실 정보 (예: T503, 5층복도, ...) (NOT NULL)
    public int anchorNUM;    // 앵커 번호 (NOT NULL)
    public string anchorTYPE;   // 앵커 타입 (normal / roomgate / way / elevator / toilet / exit)
    public string FireDt;      // 발화 여부
}

[Serializable]
public class AnchorListWrapper
{
    public System.Collections.Generic.List<AnchorRecord> anchors;
}
