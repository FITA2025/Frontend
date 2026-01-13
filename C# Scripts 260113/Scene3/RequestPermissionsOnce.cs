using UnityEngine;
using System.Collections.Generic; // List를 사용하기 위해 추가

public class RequestPermissionsOnce : MonoBehaviour
{
    // 씬이 시작될 때 한 번만 실행됩니다.
    void Start()
    {
        // 이미 허용돼 있으면 아무 것도 하지 않음
        if (OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess) &&
            OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.Scene))
        {
            return;
        }

        Debug.Log("Requesting Passthrough Camera Access Permission...");
        
        // 1. 필요한 권한 목록 생성
        var permissions = new List<OVRPermissionsRequester.Permission>();
        
        // 2. Passthrough 카메라 데이터 접근 권한 추가
        permissions.Add(OVRPermissionsRequester.Permission.PassthroughCameraAccess);
        
        // (선택적) 만약 Scene API도 사용한다면 이 권한도 추가
        permissions.Add(OVRPermissionsRequester.Permission.Scene);

        // 3. 권한 요청 팝업 띄우기
        OVRPermissionsRequester.Request(permissions.ToArray());
    }
}
