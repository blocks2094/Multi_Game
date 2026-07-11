using System;
using System.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Photon Fusion Host Mode에서
/// 방 생성, 방 참가, 방 나가기를 담당한다.
///
/// 현재 단계에서는 플레이어 생성이나 씬 이동을 처리하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FusionRoomManager : MonoBehaviour
{
    private const int RoomCodeLength = 12;

    [Header("Room Settings")]
    [Tooltip("호스트를 포함한 방의 최대 인원입니다.")]
    [SerializeField, Min(2)]
    private int maxPlayers = 8;

    [Header("UI References")]
    [SerializeField]
    private TMP_InputField roomCodeInput;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private Button createRoomButton;

    [SerializeField]
    private Button joinRoomButton;

    [SerializeField]
    private Button leaveRoomButton;

    private NetworkRunner _runner;
    private GameObject _runnerObject;

    // 중복 클릭으로 StartGame이 여러 번 호출되는 것을 방지한다.
    private bool _isStarting;

    /// <summary>
    /// 나중에 플레이어 스폰 시스템 등에서
    /// 현재 NetworkRunner를 사용할 수 있도록 제공한다.
    /// </summary>
    public NetworkRunner Runner => _runner;

    /// <summary>
    /// 현재 정상적으로 Fusion이 실행 중인지 반환한다.
    /// </summary>
    public bool IsConnected =>
        _runner != null &&
        _runner.IsRunning;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        SetStatus("방을 생성하거나 방 코드를 입력하세요.");
        SetConnectionControls(true);
        leaveRoomButton.interactable = false;
    }

    /// <summary>
    /// 방 생성 버튼에서 호출한다.
    /// </summary>
    public void CreateRoom()
    {
        if (_isStarting || IsConnected)
        {
            return;
        }

        string roomCode = GenerateRoomCode();

        // 이벤트를 발생시키지 않고 입력창의 텍스트만 변경한다.
        roomCodeInput.SetTextWithoutNotify(roomCode);

        _ = StartSessionAsync(
            GameMode.Host,
            roomCode
        );
    }

    /// <summary>
    /// 방 참가 버튼에서 호출한다.
    /// </summary>
    public void JoinRoom()
    {
        if (_isStarting || IsConnected)
        {
            return;
        }

        string roomCode = NormalizeRoomCode(
            roomCodeInput.text
        );

        if (!IsValidRoomCode(roomCode))
        {
            SetStatus(
                $"방 코드는 {RoomCodeLength}자리의 " +
                "영문 A~F와 숫자 0~9로 입력해야 합니다."
            );

            return;
        }

        roomCodeInput.SetTextWithoutNotify(roomCode);

        _ = StartSessionAsync(
            GameMode.Client,
            roomCode
        );
    }

    /// <summary>
    /// 방 나가기 버튼에서 호출한다.
    /// </summary>
    public void LeaveRoom()
    {
        if (_isStarting || _runner == null)
        {
            return;
        }

        _ = LeaveRoomAsync();
    }

    /// <summary>
    /// Host 또는 Client로 Fusion 세션을 시작한다.
    /// </summary>
    private async Task StartSessionAsync(
        GameMode gameMode,
        string roomCode)
    {
        _isStarting = true;

        SetConnectionControls(false);
        leaveRoomButton.interactable = false;

        string roleName =
            gameMode == GameMode.Host
                ? "호스트"
                : "게스트";

        SetStatus($"{roleName} 연결을 시작합니다...");

        try
        {
            CreateRunner();

            StartGameArgs startGameArgs =
                new StartGameArgs
                {
                    GameMode = gameMode,

                    // 호스트와 게스트가 동일한 방을 찾기 위한 이름
                    SessionName = roomCode,

                    // 호스트를 포함한 전체 최대 인원
                    PlayerCount = maxPlayers,

                    // 게스트가 참가할 수 있도록 방을 열어 둔다.
                    IsOpen = true,

                    // 전체 방 목록에는 노출하지 않는다.
                    // 방 코드를 알고 있는 플레이어만 참가한다.
                    IsVisible = false,

                    // 잘못된 방 코드를 입력한 Client가
                    // 새로운 빈 방을 생성하는 것을 방지한다.
                    EnableClientSessionCreation = false
                };

            StartGameResult result =
                await _runner.StartGame(startGameArgs);

            if (!result.Ok)
            {
                string errorMessage =
                    string.IsNullOrWhiteSpace(
                        result.ErrorMessage)
                        ? result.ShutdownReason.ToString()
                        : result.ErrorMessage;

                SetStatus(
                    $"{roleName} 연결 실패: {errorMessage}"
                );

                DestroyRunnerObject();
                return;
            }

            SetStatus(
                $"{roleName} 연결 성공\n" +
                $"방 코드: {roomCode}"
            );

            leaveRoomButton.interactable = true;

            Debug.Log(
                $"[FusionRoomManager] " +
                $"{roleName} 연결 성공. " +
                $"SessionName: {roomCode}"
            );
        }
        catch (Exception exception)
        {
            SetStatus(
                $"연결 중 예외가 발생했습니다: " +
                exception.Message
            );

            Debug.LogException(exception);

            DestroyRunnerObject();
        }
        finally
        {
            _isStarting = false;

            // 연결에 실패했을 때만 다시 버튼을 활성화한다.
            if (!IsConnected)
            {
                SetConnectionControls(true);
                leaveRoomButton.interactable = false;
            }
        }
    }

    /// <summary>
    /// 현재 방에서 나가고 NetworkRunner를 종료한다.
    /// </summary>
    private async Task LeaveRoomAsync()
    {
        _isStarting = true;

        SetConnectionControls(false);
        leaveRoomButton.interactable = false;
        SetStatus("방에서 나가는 중입니다...");

        NetworkRunner runnerToShutdown = _runner;

        try
        {
            if (runnerToShutdown != null &&
                !runnerToShutdown.IsShutdown)
            {
                // destroyGameObject가 true이므로
                // Shutdown이 끝나면 Runner 오브젝트도 제거된다.
                await runnerToShutdown.Shutdown(
                    destroyGameObject: true
                );
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            _runner = null;
            _runnerObject = null;
            _isStarting = false;

            SetConnectionControls(true);
            leaveRoomButton.interactable = false;

            SetStatus(
                "방에서 나왔습니다. " +
                "새 방을 생성하거나 참가할 수 있습니다."
            );
        }
    }

    /// <summary>
    /// 새로운 NetworkRunner를 생성한다.
    ///
    /// NetworkRunner는 종료되거나 연결에 실패한 뒤
    /// 재사용하지 않고 새로 생성해야 한다.
    /// </summary>
    private void CreateRunner()
    {
        if (_runner != null ||
            _runnerObject != null)
        {
            throw new InvalidOperationException(
                "이미 NetworkRunner가 생성되어 있습니다."
            );
        }

        _runnerObject =
            new GameObject("Fusion NetworkRunner");

        _runnerObject.transform.SetParent(
            transform,
            false
        );

        _runner =
            _runnerObject.AddComponent<NetworkRunner>();

        // 현재 단계에서는 네트워크 입력을 보내지 않는다.
        // 이동 시스템을 추가할 때 true로 변경한다.
        _runner.ProvideInput = false;
    }

    /// <summary>
    /// 연결 실패 등으로 남아 있는 Runner 오브젝트를 제거한다.
    /// </summary>
    private void DestroyRunnerObject()
    {
        if (_runnerObject != null)
        {
            Destroy(_runnerObject);
        }

        _runner = null;
        _runnerObject = null;
    }

    /// <summary>
    /// 충돌 가능성이 매우 낮은 무작위 방 코드를 생성한다.
    /// </summary>
    private static string GenerateRoomCode()
    {
        return Guid.NewGuid()
            .ToString("N")
            .Substring(0, RoomCodeLength)
            .ToUpperInvariant();
    }

    /// <summary>
    /// 사용자가 입력한 방 코드의 앞뒤 공백을 제거하고
    /// 소문자를 대문자로 변경한다.
    /// </summary>
    private static string NormalizeRoomCode(
        string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return string.Empty;
        }

        return roomCode
            .Trim()
            .ToUpperInvariant();
    }

    /// <summary>
    /// 생성되는 방 코드와 동일하게
    /// 0~9, A~F 문자만 허용한다.
    /// </summary>
    private static bool IsValidRoomCode(
        string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode) ||
            roomCode.Length != RoomCodeLength)
        {
            return false;
        }

        for (int i = 0; i < roomCode.Length; i++)
        {
            char character = roomCode[i];

            bool isNumber =
                character >= '0' &&
                character <= '9';

            bool isHexAlphabet =
                character >= 'A' &&
                character <= 'F';

            if (!isNumber && !isHexAlphabet)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 연결을 시작할 수 있는 UI의 활성 상태를 변경한다.
    /// </summary>
    private void SetConnectionControls(bool interactable)
    {
        createRoomButton.interactable = interactable;
        joinRoomButton.interactable = interactable;
        roomCodeInput.interactable = interactable;
    }

    private void SetStatus(string message)
    {
        statusText.text = message;
    }

    /// <summary>
    /// Inspector 연결 누락으로 인한
    /// NullReferenceException을 시작 시점에 차단한다.
    /// </summary>
    private bool ValidateReferences()
    {
        bool isValid =
            roomCodeInput != null &&
            statusText != null &&
            createRoomButton != null &&
            joinRoomButton != null &&
            leaveRoomButton != null;

        if (!isValid)
        {
            Debug.LogError(
                "[FusionRoomManager] " +
                "UI Reference가 연결되지 않았습니다. " +
                "Inspector의 모든 필드를 확인하세요.",
                this
            );
        }

        return isValid;
    }
}