using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MenuScene에서 방 생성 및 참가 UI만 담당합니다.
///
/// 네트워크 로직은 FusionNetworkManager가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private TMP_InputField roomCodeInput;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private Button createRoomButton;

    [SerializeField]
    private Button joinRoomButton;

    private FusionNetworkManager _networkManager;

    private void Start()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        _networkManager =
            FusionNetworkManager.Instance;

        if (_networkManager == null)
        {
            Debug.LogError(
                "[RoomMenuUI] " +
                "FusionNetworkManager가 없습니다.",
                this
            );

            statusText.text =
                "네트워크 매니저를 찾을 수 없습니다.";

            SetButtonsInteractable(false);
            enabled = false;
            return;
        }

        _networkManager.StatusChanged +=
            HandleStatusChanged;

        _networkManager.ConnectionFailed +=
            HandleConnectionFailed;

        statusText.text =
            "방을 생성하거나 방 코드를 입력하세요.";
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.StatusChanged -=
            HandleStatusChanged;

        _networkManager.ConnectionFailed -=
            HandleConnectionFailed;
    }

    /// <summary>
    /// 방 생성 버튼의 OnClick에 연결합니다.
    /// </summary>
    public void OnCreateRoomClicked()
    {
        if (_networkManager == null ||
            _networkManager.IsBusy)
        {
            return;
        }

        SetButtonsInteractable(false);

        string roomCode =
            _networkManager.CreateRoom();

        if (string.IsNullOrEmpty(roomCode))
        {
            SetButtonsInteractable(true);
            return;
        }

        // 연결을 시도하는 동안 생성된 코드를 보여줍니다.
        roomCodeInput.SetTextWithoutNotify(
            roomCode
        );
    }

    /// <summary>
    /// 방 참가 버튼의 OnClick에 연결합니다.
    /// </summary>
    public void OnJoinRoomClicked()
    {
        if (_networkManager == null ||
            _networkManager.IsBusy)
        {
            return;
        }

        SetButtonsInteractable(false);

        bool started =
            _networkManager.JoinRoom(
                roomCodeInput.text
            );

        if (!started)
        {
            SetButtonsInteractable(true);
        }
    }

    private void HandleStatusChanged(
        string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void HandleConnectionFailed(
        string message)
    {
        // 연결에 실패했으므로 다시 시도할 수 있게 합니다.
        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(
        bool interactable)
    {
        createRoomButton.interactable =
            interactable;

        joinRoomButton.interactable =
            interactable;

        roomCodeInput.interactable =
            interactable;
    }

    private bool ValidateReferences()
    {
        bool isValid =
            roomCodeInput != null &&
            statusText != null &&
            createRoomButton != null &&
            joinRoomButton != null;

        if (!isValid)
        {
            Debug.LogError(
                "[RoomMenuUI] " +
                "Inspector의 UI 참조가 " +
                "연결되지 않았습니다.",
                this
            );
        }

        return isValid;
    }
}