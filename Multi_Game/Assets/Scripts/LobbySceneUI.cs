using TMPro;
using UnityEngine;

/// <summary>
/// LobbyScene에 정상적으로 연결되었는지 확인하는 UI입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbySceneUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text roomCodeText;

    [SerializeField]
    private TMP_Text roleText;

    private void Start()
    {
        if (roomCodeText == null ||
            roleText == null)
        {
            Debug.LogError(
                "[LobbySceneUI] " +
                "TMP_Text 참조가 연결되지 않았습니다.",
                this
            );

            enabled = false;
            return;
        }

        FusionNetworkManager networkManager =
            FusionNetworkManager.Instance;

        if (networkManager == null ||
            networkManager.Runner == null ||
            !networkManager.Runner.IsRunning)
        {
            roomCodeText.text =
                "네트워크 연결 없음";

            roleText.text =
                "Runner 없음";

            Debug.LogError(
                "[LobbySceneUI] " +
                "실행 중인 NetworkRunner를 찾지 못했습니다.",
                this
            );

            return;
        }

        roomCodeText.text =
            $"방 코드: " +
            $"{networkManager.CurrentRoomCode}";

        roleText.text =
            networkManager.Runner.IsServer
                ? "역할: 호스트"
                : "역할: 게스트";
    }
}