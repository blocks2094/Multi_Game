using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Photon Fusion의 방 생성, 참가, NetworkRunner 수명,
/// 네트워크 씬 전환을 전담하는 매니저입니다.
///
/// 이 오브젝트는 씬이 전환되어도 파괴되지 않습니다.
/// 메뉴 UI 관련 참조는 갖지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FusionNetworkManager : MonoBehaviour
{
    private const int RoomCodeLength = 12;

    public static FusionNetworkManager Instance { get; private set; }

    [Header("Room Settings")]
    [SerializeField, Min(2)]
    private int maxPlayers = 8;

    [Header("Scene Settings")]
    [Tooltip("LobbyScene의 프로젝트 전체 경로를 입력합니다.")]
    [SerializeField]
    private string lobbyScenePath =
        "Assets/Scenes/Lobby.unity";

    [Header("Player Settings")]
    [Tooltip(
    "LobbyScene에서 생성할 플레이어 NetworkObject 프리팹입니다."
)]
    [SerializeField]
    private NetworkObject lobbyPlayerPrefab;

    private GameObject _runnerObject;
    private NetworkSceneManagerDefault _networkSceneManager;

    /// <summary>
    /// 현재 실행 중인 Fusion NetworkRunner입니다.
    /// 아직 연결하지 않았다면 null입니다.
    /// </summary>
    public NetworkRunner Runner { get; private set; }

    /// <summary>
    /// 플레이어 생성과 제거를 담당하는 Spawner입니다.
    /// </summary>
    public FusionPlayerSpawner PlayerSpawner
    {
        get;
        private set;
    }

    /// <summary>
    /// 현재 방 코드입니다.
    /// </summary>
    public string CurrentRoomCode { get; private set; }
        = string.Empty;

    /// <summary>
    /// 현재 방 생성 또는 참가 작업 중인지 나타냅니다.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// NetworkRunner가 정상적으로 실행 중인지 나타냅니다.
    /// </summary>
    public bool IsConnected =>
        Runner != null &&
        Runner.IsRunning;

    /// <summary>
    /// 연결 상태 메시지가 변경될 때 호출됩니다.
    /// 메뉴 UI가 이 이벤트를 받아 화면에 출력합니다.
    /// </summary>
    public event Action<string> StatusChanged;

    /// <summary>
    /// 방 생성 또는 참가에 실패했을 때 호출됩니다.
    /// </summary>
    public event Action<string> ConnectionFailed;

    private void Awake()
    {
        // 씬을 다시 로드했을 때
        // NetworkManager가 중복 생성되는 것을 방지합니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // MenuScene이 사라져도 이 오브젝트와
        // 자식 NetworkRunner가 함께 유지됩니다.
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 새로운 방 코드를 생성하고 Host로 방을 시작합니다.
    ///
    /// 반환값이 빈 문자열이면 시작 요청이 거절된 것입니다.
    /// </summary>
    public string CreateRoom()
    {
        if (!CanStartSession())
        {
            return string.Empty;
        }

        string roomCode = GenerateRoomCode();

        CurrentRoomCode = roomCode;

        StartSession(
            GameMode.Host,
            roomCode
        );

        return roomCode;
    }

    /// <summary>
    /// 입력받은 방 코드로 Client 참가를 시작합니다.
    ///
    /// true면 참가 요청이 정상적으로 시작된 것이고,
    /// false면 코드가 잘못됐거나 이미 연결 중인 상태입니다.
    /// </summary>
    public bool JoinRoom(string rawRoomCode)
    {
        if (!CanStartSession())
        {
            return false;
        }

        string roomCode =
            NormalizeRoomCode(rawRoomCode);

        if (!IsValidRoomCode(roomCode))
        {
            ReportFailure(
                $"방 코드는 {RoomCodeLength}자리의 " +
                "숫자 0~9와 영문 A~F로 입력해야 합니다."
            );

            return false;
        }

        CurrentRoomCode = roomCode;

        StartSession(
            GameMode.Client,
            roomCode
        );

        return true;
    }

    private bool CanStartSession()
    {
        if (IsBusy)
        {
            StatusChanged?.Invoke(
                "현재 연결을 처리하고 있습니다."
            );

            return false;
        }

        if (IsConnected)
        {
            StatusChanged?.Invoke(
                "이미 방에 연결되어 있습니다."
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// 비동기 작업을 안전하게 시작합니다.
    ///
    /// 실제 예외는 StartSessionAsync 안에서 모두 처리하므로
    /// fire-and-forget 형태로 호출해도 예외가 유실되지 않습니다.
    /// </summary>
    private void StartSession(
        GameMode gameMode,
        string roomCode)
    {
        _ = StartSessionAsync(
            gameMode,
            roomCode
        );
    }

    private async Task StartSessionAsync(
        GameMode gameMode,
        string roomCode)
    {
        IsBusy = true;

        string roleName =
            gameMode == GameMode.Host
                ? "호스트"
                : "게스트";

        StatusChanged?.Invoke(
            $"{roleName} 연결을 시작합니다..."
        );

        try
        {
            int lobbySceneIndex =
                GetLobbySceneBuildIndex();

            if (lobbySceneIndex < 0)
            {
                ReportFailure(
                    "LobbyScene이 Build Profiles의 " +
                    "Scene List에 등록되지 않았습니다.\n" +
                    $"확인 경로: {lobbyScenePath}"
                );

                CurrentRoomCode = string.Empty;
                return;
            }

            if (lobbyPlayerPrefab == null)
            {
                CurrentRoomCode = string.Empty;

                ReportFailure(
                    "Lobby Player Prefab이 연결되지 않았습니다. " +
                    "FusionNetworkManager Inspector를 확인하세요."
                );

                return;
            }

            CreateRunner();

            StartGameArgs startGameArgs =
                new StartGameArgs
                {
                    GameMode = gameMode,

                    SessionName = roomCode,

                    PlayerCount = maxPlayers,

                    // 게스트가 참가할 수 있는 열린 방
                    IsOpen = true,

                    // 공개 방 목록에는 표시하지 않음
                    IsVisible = false,

                    // 잘못된 방 코드를 입력한 게스트가
                    // 새로운 방을 생성하지 못하도록 방지
                    EnableClientSessionCreation = false,

                    // 호스트가 시작할 네트워크 씬
                    Scene = SceneRef.FromIndex(
                        lobbySceneIndex
                    ),

                    // Fusion 네트워크 씬 관리자
                    SceneManager = _networkSceneManager
                };

            StartGameResult result =
                await Runner.StartGame(startGameArgs);

            if (!result.Ok)
            {
                string errorMessage =
                    string.IsNullOrWhiteSpace(
                        result.ErrorMessage)
                        ? result.ShutdownReason.ToString()
                        : result.ErrorMessage;

                await CleanupRunnerAsync();

                CurrentRoomCode = string.Empty;

                ReportFailure(
                    $"{roleName} 연결 실패: " +
                    errorMessage
                );

                return;
            }

            /*
             * StartGame이 성공하면:
             *
             * Host:
             * LobbyScene을 네트워크 씬으로 로드합니다.
             *
             * Client:
             * 자신이 지정한 씬을 독립적으로 사용하는 것이 아니라
             * Host가 사용 중인 네트워크 씬으로 동기화됩니다.
             */

            StatusChanged?.Invoke(
                $"{roleName} 연결 성공"
            );

            Debug.Log(
                $"[FusionNetworkManager] " +
                $"{roleName} 연결 성공 / " +
                $"Room: {roomCode}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            await CleanupRunnerAsync();

            CurrentRoomCode = string.Empty;

            ReportFailure(
                "네트워크 연결 중 예외가 발생했습니다.\n" +
                exception.Message
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// NetworkRunner와 NetworkSceneManagerDefault를 생성합니다.
    ///
    /// NetworkRunner는 연결 종료 후 재사용할 수 없으므로
    /// 세션을 시작할 때마다 새로 생성합니다.
    /// </summary>
    private void CreateRunner()
    {
        if (Runner != null ||
        _runnerObject != null)
        {
            throw new InvalidOperationException(
                "NetworkRunner가 이미 생성되어 있습니다."
            );
        }

        _runnerObject =
            new GameObject("Fusion NetworkRunner");

        /*
         * 모든 Fusion 컴포넌트를 먼저 추가한 다음 활성화합니다.
         * Runner 초기화 시점에 FusionPlayerSpawner가
         * 확실히 존재하도록 만들기 위한 처리입니다.
         */
        _runnerObject.SetActive(false);

        _runnerObject.transform.SetParent(
            transform,
            false
        );

        Runner =
            _runnerObject.AddComponent<NetworkRunner>();

        _networkSceneManager =
            _runnerObject.AddComponent
                <NetworkSceneManagerDefault>();

        PlayerSpawner =
            _runnerObject.AddComponent
                <FusionPlayerSpawner>();

        PlayerSpawner.Initialize(
            lobbyPlayerPrefab,
            maxPlayers
        );

        Runner.ProvideInput = false;

        _runnerObject.SetActive(true);
    }

    /// <summary>
    /// 실패한 NetworkRunner를 안전하게 종료하고 제거합니다.
    /// </summary>
    private async Task CleanupRunnerAsync()
    {
        NetworkRunner runnerToCleanup = Runner;
        GameObject objectToCleanup = _runnerObject;

        Runner = null;
        _runnerObject = null;
        _networkSceneManager = null;
        PlayerSpawner = null;

        if (runnerToCleanup != null &&
            !runnerToCleanup.IsShutdown)
        {
            try
            {
                await runnerToCleanup.Shutdown(
                    destroyGameObject: true
                );

                return;
            }
            catch (Exception shutdownException)
            {
                Debug.LogException(shutdownException);
            }
        }

        if (objectToCleanup != null)
        {
            Destroy(objectToCleanup);
        }
    }

    private int GetLobbySceneBuildIndex()
    {
        if (string.IsNullOrWhiteSpace(
                lobbyScenePath))
        {
            return -1;
        }

        return SceneUtility
            .GetBuildIndexByScenePath(
                lobbyScenePath.Trim()
            );
    }

    private void ReportFailure(string message)
    {
        StatusChanged?.Invoke(message);
        ConnectionFailed?.Invoke(message);

        Debug.LogError(
            $"[FusionNetworkManager] {message}"
        );
    }

    private static string GenerateRoomCode()
    {
        return Guid.NewGuid()
            .ToString("N")
            .Substring(0, RoomCodeLength)
            .ToUpperInvariant();
    }

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

    private static bool IsValidRoomCode(
        string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode) ||
            roomCode.Length != RoomCodeLength)
        {
            return false;
        }

        for (int i = 0;
             i < roomCode.Length;
             i++)
        {
            char character = roomCode[i];

            bool isNumber =
                character >= '0' &&
                character <= '9';

            bool isHexAlphabet =
                character >= 'A' &&
                character <= 'F';

            if (!isNumber &&
                !isHexAlphabet)
            {
                return false;
            }
        }

        return true;
    }
}