// 260320 최신화
// 최신화 내용: Proxy Anchor 고정을 위한 좌표 필드 추가

using System;
using UnityEngine;

[Serializable]
public class AnchorRecord
{
    public string uuid;          // Meta Spatial Anchor의 고유 ID (NOT NULL, 메타퀘스트에서 자동으로 등록하는 것이라 임의 수정 불가)
    public int floor;           // 층 정보 (1-10) (NOT NULL)
    public string roomId;       // 호실 정보 (예: T503, 5층복도, ...) (NOT NULL)
    public int anchorNUM;    // 앵커 번호 (NOT NULL)
    public string anchorTYPE;   // 앵커 타입 (normal / roomgate / way / elevator / toilet / exit)
    public string FireDt;      // 발화 여부

    // --- 추가: Serialize 가능한 좌표 필드 ---
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ, rotW;

    // 유니티 엔진에서 바로 사용할 수 있는 헬퍼 프로퍼티
    public Vector3 LocalPos => new Vector3(posX, posY, posZ);
    public Quaternion LocalRot => new Quaternion(rotX, rotY, rotZ, rotW);
}

[Serializable]
public class AnchorListWrapper
{
    public System.Collections.Generic.List<AnchorRecord> anchors;
}
