using Fusion;
using UnityEngine;

/// <summary>
/// 네트워크에서 생성된 로비 플레이어를 나타냅니다.
///
/// 현재 단계에서는 플레이어 생성 및 권한 확인만 담당합니다.
/// 이동과 카메라는 다음 단계에서 추가합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LobbyPlayer : NetworkBehaviour
{
    /// <summary>
    /// 이 컴퓨터의 로컬 플레이어 오브젝트인지 반환합니다.
    /// </summary>
    public bool IsLocalPlayer =>
        Object != null &&
        Object.HasInputAuthority;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            gameObject.name = "LobbyPlayer_Local";

            Debug.Log(
                "[LobbyPlayer] " +
                "내 플레이어 오브젝트가 생성되었습니다.",
                this
            );
        }
        else
        {
            gameObject.name =
                $"LobbyPlayer_Remote_{Object.InputAuthority.RawEncoded}";

            Debug.Log(
                "[LobbyPlayer] " +
                "다른 플레이어 오브젝트가 생성되었습니다.",
                this
            );
        }
    }
}