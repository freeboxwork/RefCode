using System.Threading.Tasks;
using UnityEngine;
using SMOE.LawCourt;

public class LawCourtTestManager : UIBase
{
    [Header("🧪 Test Manager")]
    [Tooltip("법원 시스템 테스트용 매니저")]
    public bool isInitialized = true;

    /// <summary>
    /// 네트워크 매니저 참조
    /// </summary>
    private SMOE.LawCourt.LawCourtNetworkManager networkManager;

    public static async Task<LawCourtTestManager> CreateAsync()
    {
        var prefab = await AddressablesHelper.LoadPrefabAsync<LawCourtTestManager>("Assets/Prefabs/UI/LawCourt/LawCourtTestManager.prefab");

        if (prefab == null) return null;

        var canvas = UIManager.Instance.MainCanvas.transform;
        var courtScript = Instantiate(prefab, canvas);
        courtScript.name = "LawCourtTestManager";
        courtScript.InitRect(AnchorMode.Stretch);

        return courtScript;
    }

    public static void Destroy()
    {
        var canvas = UIManager.Instance.MainCanvas;
        var courtScript = canvas.GetComponentInChildren<LawCourtTestManager>();
        if (courtScript != null)
        {
            courtScript.DestroyInternal();
        }
    }

    private void DestroyInternal()
    {
        Hide();
    }



    #region Unity Lifecycle
    protected override void Start()
    {
        base.Start();
        Debug.Log("LawCourtTestManager 초기화 완료");

        // 네트워크 매니저 초기화
        InitializeNetworkManager();
    }

    protected override void OnDestroy()
    {
        // 네트워크 이벤트 구독 해제
        UnsubscribeFromNetworkEvents();
        base.OnDestroy();
    }
    #endregion

    #region Network Integration

    /// <summary>
    /// 네트워크 매니저 초기화 및 이벤트 구독
    /// </summary>
    private void InitializeNetworkManager()
    {
        // 네트워크 매니저 찾기
        networkManager = FindFirstObjectByType<SMOE.LawCourt.LawCourtNetworkManager>();

        if (networkManager != null)
        {
            Debug.Log("🌐 LawCourtNetworkManager 연결됨 (TestManager)");
            SubscribeToNetworkEvents();
        }
        else
        {
            Debug.LogWarning("⚠️ LawCourtNetworkManager를 찾을 수 없습니다. 로컬 모드로 작동합니다.");
        }
    }

    /// <summary>
    /// 네트워크 이벤트 구독
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (networkManager == null) return;

        networkManager.OnCreateReviewManagerRequested += OnNetworkCreateReviewManagerRequested;
        networkManager.OnCreateCourtScriptControllerRequested += OnNetworkCreateCourtScriptControllerRequested;
        networkManager.OnCreateAllCourtComponentsRequested += OnNetworkCreateAllCourtComponentsRequested;

        Debug.Log("🔗 네트워크 이벤트 구독 완료 (TestManager)");
    }

    /// <summary>
    /// 네트워크 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromNetworkEvents()
    {
        if (networkManager == null) return;

        networkManager.OnCreateReviewManagerRequested -= OnNetworkCreateReviewManagerRequested;
        networkManager.OnCreateCourtScriptControllerRequested -= OnNetworkCreateCourtScriptControllerRequested;
        networkManager.OnCreateAllCourtComponentsRequested -= OnNetworkCreateAllCourtComponentsRequested;

        Debug.Log("🔗 네트워크 이벤트 구독 해제 완료 (TestManager)");
    }

    /// <summary>
    /// 네트워크에서 리뷰 매니저 생성 요청 받았을 때
    /// </summary>
    private async void OnNetworkCreateReviewManagerRequested(string triggeredByUserId)
    {
        Debug.Log($"🌐 네트워크 리뷰 매니저 생성 요청 받음: by {triggeredByUserId}");
        Debug.Log($"🌐 현재 사용자 ID: {PlayData.User?.Id}");
        Debug.Log($"🌐 요청자와 같은 사용자인가: {PlayData.User?.Id == triggeredByUserId}");

        var result = await CreateReviewManagerInternal();

        if (result != null)
        {
            Debug.Log($"🎉 네트워크 동기화로 리뷰 매니저 생성 완료 (요청자: {triggeredByUserId})");
        }
        else
        {
            Debug.LogError($"❌ 네트워크 동기화 리뷰 매니저 생성 실패 (요청자: {triggeredByUserId})");
        }
    }

    /// <summary>
    /// 네트워크에서 법원 스크립트 컨트롤러 생성 요청 받았을 때
    /// </summary>
    private async void OnNetworkCreateCourtScriptControllerRequested(string triggeredByUserId)
    {
        if (triggeredByUserId != PlayData.User.Id)
            return;

        Debug.Log($"🌐 네트워크 법원 스크립트 컨트롤러 생성 요청");
        await CreateCourtScriptControllerInternal();
    }

    /// <summary>
    /// 네트워크에서 모든 법원 컴포넌트 생성 요청 받았을 때
    /// </summary>
    private async void OnNetworkCreateAllCourtComponentsRequested(string triggeredByUserId)
    {
        Debug.Log($"🌐 네트워크 모든 법원 컴포넌트 생성 요청: by {triggeredByUserId}");
        await CreateAllCourtComponentsInternal();
    }

    #endregion

    #region Context Menu Functions
    /// <summary>
    /// 리뷰 매니저 생성 - 컨텍스트 메뉴에서 실행 가능 (네트워크 동기화)
    /// </summary>
    [ContextMenu("🔧 Create Review Manager (Network Sync)")]
    public void CreateReviewManager()
    {
        Debug.Log("🔄 LawCourtReviewManager 생성 중... (네트워크 동기화)");

        // 네트워크 매니저 상태 디버깅
        DebugNetworkManagerState();

        if (networkManager != null)
        {
            string userId = PlayData.User?.Id ?? "";
            Debug.Log($"🌐 RPC 호출 시도: userId={userId}");

            // NetworkObject 상태 확인
            if (networkManager.Object != null)
            {
                Debug.Log($"🌐 NetworkObject 상태: IsValid={networkManager.Object.IsValid}, HasStateAuthority={networkManager.Object.HasStateAuthority}");

                networkManager.RPC_RequestCreateReviewManager(userId);
                Debug.Log("🌐 네트워크를 통해 리뷰 매니저 생성 요청 완료");
            }
            else
            {
                Debug.LogError("❌ NetworkObject가 null입니다!");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ 네트워크 매니저가 없어 로컬에서만 생성");
            _ = CreateReviewManagerInternal();
        }
    }

    /// <summary>
    /// 네트워크 매니저 상태 디버깅
    /// </summary>
    private void DebugNetworkManagerState()
    {
        Debug.Log("=== 네트워크 매니저 상태 디버깅 ===");
        Debug.Log($"NetworkManager 존재: {networkManager != null}");

        if (networkManager != null)
        {
            Debug.Log($"NetworkManager Instance: {networkManager.name}");
            Debug.Log($"NetworkObject: {networkManager.Object != null}");

            if (networkManager.Object != null)
            {
                Debug.Log($"IsValid: {networkManager.Object.IsValid}");
                Debug.Log($"HasStateAuthority: {networkManager.Object.HasStateAuthority}");
                Debug.Log($"HasInputAuthority: {networkManager.Object.HasInputAuthority}");
                Debug.Log($"Runner: {networkManager.Object.Runner != null}");
            }
        }

        // 모든 NetworkManager 찾기
        var allNetworkManagers = FindObjectsOfType<SMOE.LawCourt.LawCourtNetworkManager>();
        Debug.Log($"씬에 있는 LawCourtNetworkManager 개수: {allNetworkManagers.Length}");

        for (int i = 0; i < allNetworkManagers.Length; i++)
        {
            var nm = allNetworkManagers[i];
            Debug.Log($"NetworkManager[{i}]: {nm.name}, IsValid: {nm.Object?.IsValid}");
        }

        Debug.Log("=== 디버깅 완료 ===");
    }

    /// <summary>
    /// 법원 스크립트 컨트롤러 생성 - 컨텍스트 메뉴에서 실행 가능 (네트워크 동기화)
    /// </summary>
    [ContextMenu("🔧 Create Court Script Controller (Network Sync)")]
    public void CreateCourtScriptController()
    {
        Debug.Log("🔄 CourtScriptController 생성 중... (네트워크 동기화)");

        if (networkManager != null)
        {
            string userId = PlayData.User?.Id ?? "";
            networkManager.RPC_RequestCreateCourtScriptController(userId);
            Debug.Log("🌐 네트워크를 통해 법원 스크립트 컨트롤러 생성 요청");
        }
        else
        {
            Debug.LogWarning("⚠️ 네트워크 매니저가 없어 로컬에서만 생성");
            _ = CreateCourtScriptControllerInternal();
        }
    }

    /// <summary>
    /// 모든 법원 UI 컴포넌트 생성 (네트워크 동기화)
    /// </summary>
    [ContextMenu("🚀 Create All Court Components (Network Sync)")]
    public void CreateAllCourtComponents()
    {
        Debug.Log("🔄 모든 법원 컴포넌트 생성 중... (네트워크 동기화)");

        if (networkManager != null)
        {
            string userId = PlayData.User?.Id ?? "";
            networkManager.RPC_RequestCreateAllCourtComponents(userId);
            Debug.Log("🌐 네트워크를 통해 모든 법원 컴포넌트 생성 요청");
        }
        else
        {
            Debug.LogWarning("⚠️ 네트워크 매니저가 없어 로컬에서만 생성");
            _ = CreateAllCourtComponentsInternal();
        }
    }
    #endregion

    #region Internal Component Creation Methods

    /// <summary>
    /// 리뷰 매니저 내부 생성 (실제 실행)
    /// </summary>
    private async Task<LawCourtReviewManager> CreateReviewManagerInternal()
    {
        try
        {
            var reviewManager = await LawCourtReviewManager.CreateAsync();

            if (reviewManager != null)
            {
                Debug.Log("✅ LawCourtReviewManager 생성 성공!");

                // 현재 법원 ID가 있으면 자동으로 설정
                if (PlayData.Instance.CurrentLawCourt != null)
                {
                    reviewManager.lawCourtId = PlayData.Instance.CurrentLawCourt.Id;
                    Debug.Log($"🏛️ 리뷰 매니저에 법원 ID 설정: {reviewManager.lawCourtId}");
                }

                return reviewManager;
            }
            else
            {
                Debug.LogError("❌ LawCourtReviewManager 생성 실패");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ LawCourtReviewManager 생성 오류: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 법원 스크립트 컨트롤러 내부 생성 (실제 실행)
    /// </summary>
    private async Task<CourtScriptController> CreateCourtScriptControllerInternal()
    {
        try
        {
            var courtController = await CourtScriptController.CreateAsync();

            if (courtController != null)
            {
                Debug.Log("✅ CourtScriptController 생성 성공!");
                return courtController;
            }
            else
            {
                Debug.LogError("❌ CourtScriptController 생성 실패");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ CourtScriptController 생성 오류: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 모든 법원 컴포넌트 내부 생성 (실제 실행)
    /// </summary>
    private async Task CreateAllCourtComponentsInternal()
    {
        try
        {
            // 리뷰 매니저 생성
            var reviewManager = await CreateReviewManagerInternal();

            // 법원 스크립트 컨트롤러 생성
            var courtController = await CreateCourtScriptControllerInternal();

            if (reviewManager != null && courtController != null)
            {
                Debug.Log("✅ 모든 법원 컴포넌트 생성 성공!");
            }
            else
            {
                Debug.LogWarning("⚠️ 일부 컴포넌트 생성 실패");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ 법원 컴포넌트 생성 오류: {ex.Message}");
        }
    }

    #endregion

    #region Public Methods
    /// <summary>
    /// 프로그래매틱하게 리뷰 매니저 생성 (네트워크 동기화)
    /// </summary>
    public void CreateReviewManagerNetworked()
    {
        Debug.Log("🔄 리뷰 매니저 네트워크 생성 중...");
        CreateReviewManager();
    }

    /// <summary>
    /// 프로그래매틱하게 법원 스크립트 컨트롤러 생성 (네트워크 동기화)
    /// </summary>
    public void CreateCourtScriptControllerNetworked()
    {
        Debug.Log("🔄 법원 스크립트 컨트롤러 네트워크 생성 중...");
        CreateCourtScriptController();
    }

    /// <summary>
    /// 프로그래매틱하게 리뷰 매니저 생성 (로컬 전용)
    /// </summary>
    public async Task<LawCourtReviewManager> CreateReviewManagerAsync()
    {
        Debug.Log("🔄 리뷰 매니저 로컬 생성 중...");
        return await CreateReviewManagerInternal();
    }

    /// <summary>
    /// 프로그래매틱하게 법원 스크립트 컨트롤러 생성 (로컬 전용)
    /// </summary>
    public async Task<CourtScriptController> CreateCourtScriptControllerAsync()
    {
        Debug.Log("🔄 법원 스크립트 컨트롤러 로컬 생성 중...");
        return await CreateCourtScriptControllerInternal();
    }

    /// <summary>
    /// 모든 법원 컴포넌트 생성 (네트워크 동기화)
    /// </summary>
    public void CreateAllCourtComponentsNetworked()
    {
        Debug.Log("🔄 모든 법원 컴포넌트 네트워크 생성 중...");
        CreateAllCourtComponents();
    }

    /// <summary>
    /// 모든 법원 컴포넌트 생성 (로컬 전용)
    /// </summary>
    public async Task CreateAllCourtComponentsAsync()
    {
        Debug.Log("🔄 모든 법원 컴포넌트 로컬 생성 중...");
        await CreateAllCourtComponentsInternal();
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// 현재 생성된 법원 컴포넌트 상태 확인
    /// </summary>
    [ContextMenu("📊 Check Court Components Status")]
    public void CheckCourtComponentsStatus()
    {
        Debug.Log("🔍 법원 컴포넌트 상태 확인 중...");

        var canvas = UIManager.Instance.MainCanvas;

        // 리뷰 매니저 확인
        var reviewManager = canvas.GetComponentInChildren<LawCourtReviewManager>();
        Debug.Log($"LawCourtReviewManager: {(reviewManager != null ? "✅ 존재" : "❌ 없음")}");

        // 법원 스크립트 컨트롤러 확인
        var courtController = canvas.GetComponentInChildren<CourtScriptController>();
        Debug.Log($"CourtScriptController: {(courtController != null ? "✅ 존재" : "❌ 없음")}");
    }
    #endregion
}