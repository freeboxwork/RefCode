using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LawCourtReviewManager : UIBase
{
    #region Settings
    [Header("⚙️ Basic Settings")]
    [Tooltip("법원 ID - API 호출 시 사용되는 고유 식별자")]
    public string lawCourtId = "";
    #endregion

    #region Review Creation UI
    [Space(10)]
    [Header("📝 Review Creation")]
    [Tooltip("리뷰 텍스트 입력 필드")]
    public TMP_InputField reviewInputField;

    [Tooltip("리뷰 생성 버튼 (업로드 팝업 표시)")]
    public Button reviewCreateButton;

    [Space(5)]
    [Tooltip("후기 작성 팝업 GameObject")]
    public GameObject createReviewPopup;

    [Tooltip("후기 작성 취소 버튼")]
    public Button btnCreateReviewCancel;

    [Tooltip("후기 작성 팝업 열기 버튼")]
    public Button bntCreateReview;
    #endregion

    #region Review List UI
    [Space(10)]
    [Header("📋 Review List")]
    [Tooltip("리뷰 개수 표시 텍스트")]
    public TextMeshProUGUI txtReviewCount;

    [Tooltip("리뷰 아이템 프리팹")]
    public LawCourtReviewItem reviewItemPrefab;

    [Tooltip("리뷰 아이템들이 생성될 부모 Transform")]
    public Transform reviewItemParent;

    [Space(5)]
    [Tooltip("리뷰 목록 팝업 GameObject")]
    public GameObject reviewListPopup;
    public Button btnReviewListClose;

    [Tooltip("리뷰 목록 새로고침 버튼")]
    public Button btnGetReviews;



    [Space(5)]
    [Tooltip("현재 로드된 리뷰 아이템 목록 (디버깅용)")]
    [HideInInspector]
    public List<LawCourtReviewItem> reviewItems = new List<LawCourtReviewItem>();
    #endregion

    #region Review Edit UI
    [Space(10)]
    [Header("✏️ Review Edit")]
    [Tooltip("리뷰 수정용 텍스트 입력 필드")]
    public TMP_InputField editReviewInputField;

    [Tooltip("리뷰 수정 팝업 GameObject")]
    public GameObject editReviewPopup;

    [Space(5)]
    [Tooltip("리뷰 수정 저장 버튼")]
    public Button btnEditReviewSave;

    [Tooltip("리뷰 수정 취소 버튼")]
    public Button btnEditReviewCancel;

    [Tooltip("리뷰 수정 팝업 열기 버튼")]
    public Button btnShowEditReviewPopup;
    #endregion

    #region Review Delete UI
    [Space(10)]
    [Header("🗑️ Review Delete")]
    [Tooltip("리뷰 삭제 확인 팝업 GameObject")]
    public GameObject deleteReviewPopup;

    [Space(5)]
    [Tooltip("리뷰 삭제 확인 버튼")]
    public Button btnDeleteReview;

    [Tooltip("리뷰 삭제 취소 버튼")]
    public Button btnDeleteReviewCancel;

    [Tooltip("리뷰 삭제 팝업 열기 버튼")]
    public Button btnShowDeleteReviewPopup;
    #endregion

    #region Review Upload UI
    [Space(10)]
    [Header("📤 Review Upload")]
    [Tooltip("리뷰 업로드 팝업 GameObject")]
    public GameObject reviewUploadPopup;

    [Space(5)]
    [Tooltip("리뷰 업로드 실행 버튼")]
    public Button btnReviewUpload;

    [Tooltip("리뷰 업로드 취소 버튼")]
    public Button btnReviewUploadCancel;
    #endregion

    #region Edit Popup UI
    [Space(10)]
    [Header("🔧 Edit Actions Popup")]
    [Tooltip("수정/삭제 액션 선택 팝업 GameObject")]
    public GameObject editPopup;
    #endregion

    #region Private Variables
    [Space(10)]
    [Header("🔒 Internal State (Debug Only)")]
    [Tooltip("현재 수정 중인 리뷰 아이템")]
    [SerializeField] private LawCourtReviewItem currentEditingItem;

    /// <summary>
    /// 현재 수정 중인 리뷰 ID (참조 복원용)
    /// </summary>
    [SerializeField] private string currentEditingReviewId = "";

    /// <summary>
    /// 임시 저장된 리뷰 텍스트
    /// </summary>
    [SerializeField] private string tempReviewText = "";


    // 단순 비활성화 이미지 ( 리뷰 버튼 )
    public GameObject imgReviewOff;

    public GameObject courtEndAlertAnim;


    /// <summary>
    /// 네트워크 매니저 참조
    /// </summary>
    private SMOE.LawCourt.LawCourtNetworkManager networkManager;
    #endregion

    public static async Task<LawCourtReviewManager> CreateAsync()
    {
        var prefab = await AddressablesHelper.LoadPrefabAsync<LawCourtReviewManager>("Assets/Prefabs/UI/LawCourt/LawCourtReviewManager.prefab");

        if (prefab == null) return null;

        var canvas = UIManager.Instance.MainCanvas.transform;
        var courtScript = Instantiate(prefab, canvas);
        courtScript.name = "LawCourtReviewManager";
        courtScript.InitRect(AnchorMode.Stretch);

        return courtScript;
    }

    public static void Destroy()
    {
        var canvas = UIManager.Instance.MainCanvas;
        var courtScript = canvas.GetComponentInChildren<LawCourtReviewManager>();
        if (courtScript != null)
        {
            courtScript.DestroyInternal();
        }
    }

    private void DestroyInternal()
    {
        Hide();
    }

    // 재판 다시하기 버튼 클릭시    
    void ResetUI()
    {


        // // 리뷰 아이템들 제거
        // foreach (var item in reviewItems)
        // {
        //     Destroy(item.gameObject);
        // }

        // // 리뷰 목록 초기화
        // reviewItems.Clear();
        reviewListPopup.SetActive(false);
        imgReviewOff.SetActive(true);

        // 편집 상태 초기화
        currentEditingItem = null;
        currentEditingReviewId = "";

        // Queue 시스템 리셋 요청
        if (networkManager != null)
        {
            Debug.Log("🔄 리뷰 Queue 시스템 리셋 요청");
        }
    }

    public void DisableReviewOff(bool value)
    {
        imgReviewOff.SetActive(!value);
    }


    #region Unity Lifecycle
    protected override void Start()
    {
        base.Start();
        SetEvent();

        if (PlayData.Instance != null)
        {
            lawCourtId = PlayData.Instance.CurrentLawCourt.Id;
        }



        // 네트워크 매니저 찾기 및 연동
        InitializeNetworkManager();
    }

    protected override void OnDestroy()
    {
        RemoveEvent();
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
            Debug.Log("LawCourtNetworkManager 연결됨");
            SubscribeToNetworkEvents();
        }
        else
        {
            Debug.LogWarning("LawCourtNetworkManager를 찾을 수 없습니다. 로컬 모드로 작동합니다.");
        }
    }

    /// <summary>
    /// 네트워크 이벤트 구독
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (networkManager == null) return;

        networkManager.OnReviewListRefreshRequested += OnNetworkReviewListRefreshRequested;
        networkManager.OnReviewModeChanged += OnNetworkReviewModeChanged;
        networkManager.OnReviewIndexChanged += OnNetworkReviewIndexChanged;

        Debug.Log("네트워크 이벤트 구독 완료");
    }

    /// <summary>
    /// 네트워크 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromNetworkEvents()
    {
        if (networkManager == null) return;

        networkManager.OnReviewListRefreshRequested -= OnNetworkReviewListRefreshRequested;
        networkManager.OnReviewModeChanged -= OnNetworkReviewModeChanged;
        networkManager.OnReviewIndexChanged -= OnNetworkReviewIndexChanged;

        Debug.Log("네트워크 이벤트 구독 해제 완료");
    }

    /// <summary>
    /// 네트워크에서 리뷰 목록 새로고침 요청 받았을 때
    /// </summary>
    private void OnNetworkReviewListRefreshRequested(string courtId, string triggeredByUserId, string action)
    {
        // 안전성 체크
        if (this == null || gameObject == null) return;

        // 현재 법원 ID와 일치하는 경우만 새로고침
        if (courtId == lawCourtId)
        {
            Debug.Log($"🔄 네트워크 리뷰 목록 새로고침: action={action}, user={triggeredByUserId}");

            // 메인 스레드에서 안전하게 실행
            StartCoroutine(SafeRefreshReviewList(courtId, action));
        }
    }

    /// <summary>
    /// 안전한 리뷰 목록 새로고침
    /// </summary>
    private IEnumerator SafeRefreshReviewList(string courtId, string action)
    {

        // UI 업데이트 전 짧은 대기
        yield return new WaitForSeconds(0.1f);

        // 기존 리뷰 목록 새로고침 로직 실행
        LoadReviewList();

        Debug.Log($"✅ 안전한 리뷰 목록 새로고침 완료: {action}");
    }

    /// <summary>
    /// 네트워크에서 리뷰 모드 변경 알림
    /// </summary>
    private async void OnNetworkReviewModeChanged(bool reviewMode)
    {



        Debug.Log($"네트워크 리뷰 모드 변경: {reviewMode}");
        // 필요시 UI 상태 업데이트
        DisableReviewOff(reviewMode);
        if (reviewMode)
        {
            // 재판 종료 알림 애니메이션
            courtEndAlertAnim.SetActive(true);
            await Task.Delay(3300);

            Debug.Log("리뷰 모드 : " + reviewMode);
            btnGetReviews.onClick.Invoke();
        }
    }



    /// <summary>
    /// 네트워크에서 리뷰 인덱스 변경 알림
    /// </summary>
    private void OnNetworkReviewIndexChanged(int reviewIndex)
    {
        Debug.Log($"네트워크 리뷰 인덱스 변경: {reviewIndex}");
        // 필요시 UI 상태 업데이트
    }

    /// <summary>
    /// 리뷰 생성 네트워크 알림
    /// </summary>
    private void NotifyReviewCreated(string reviewId)
    {
        // 안전성 체크
        if (this == null || networkManager == null || string.IsNullOrEmpty(lawCourtId)) return;

        string userId = PlayData.User?.Id ?? "";
        networkManager.RPC_NotifyReviewCreated(lawCourtId, userId, reviewId);
        Debug.Log($"✅ 안전한 리뷰 생성 알림: {reviewId}");
    }

    /// <summary>
    /// 리뷰 수정 네트워크 알림
    /// </summary>
    private void NotifyReviewUpdated(string reviewId)
    {
        // 안전성 체크
        if (this == null || networkManager == null || string.IsNullOrEmpty(lawCourtId)) return;

        string userId = PlayData.User?.Id ?? "";
        networkManager.RPC_NotifyReviewUpdated(lawCourtId, userId, reviewId);
        Debug.Log($"✅ 안전한 리뷰 수정 알림: {reviewId}");
    }

    /// <summary>
    /// 리뷰 삭제 네트워크 알림
    /// </summary>
    private void NotifyReviewDeleted(string reviewId)
    {
        // 안전성 체크
        if (this == null || networkManager == null || string.IsNullOrEmpty(lawCourtId)) return;

        string userId = PlayData.User?.Id ?? "";
        networkManager.RPC_NotifyReviewDeleted(lawCourtId, userId, reviewId);
        Debug.Log($"✅ 안전한 리뷰 삭제 알림: {reviewId}");
    }

    #endregion

    #region Event Management
    void SetEvent()
    {
        // 재판 다시하기 버튼 클릭시 이벤트 구독
        GlobalEvent.OnLawCourtPrepareTrial.Subscribe(ResetUI);

        // 리뷰 생성 관련 이벤트
        reviewCreateButton.onClick.AddListener(ShowReviewUploadPopup);
        btnCreateReviewCancel.onClick.AddListener(() => createReviewPopup.SetActive(false));
        bntCreateReview.onClick.AddListener(() => createReviewPopup.SetActive(true));

        // 리뷰 목록 관련 이벤트
        btnGetReviews.onClick.AddListener(() =>
        {
            reviewListPopup.SetActive(true);
            LoadReviewList();
        });

        btnReviewListClose.onClick.AddListener(() =>
        {
            reviewListPopup.SetActive(false);
        });

        // 리뷰 수정 관련 이벤트
        if (btnEditReviewSave != null)
            btnEditReviewSave.onClick.AddListener(SaveEditedReview);

        if (btnEditReviewCancel != null)
            btnEditReviewCancel.onClick.AddListener(CancelEditReview);

        if (btnShowEditReviewPopup != null)
        {
            btnShowEditReviewPopup.onClick.AddListener(() =>
            {
                Debug.Log($"🔄 수정 버튼 클릭: currentEditingItem={currentEditingItem}");
                editPopup.SetActive(false);
                ShowEditReviewPopup();
            });
        }

        // 리뷰 삭제 관련 이벤트
        if (btnDeleteReview != null)
            btnDeleteReview.onClick.AddListener(DeleteReview);

        if (btnDeleteReviewCancel != null)
            btnDeleteReviewCancel.onClick.AddListener(CancelDeleteReview);

        if (btnShowDeleteReviewPopup != null)
        {
            btnShowDeleteReviewPopup.onClick.AddListener(() =>
            {
                editPopup.SetActive(false);
                ShowDeleteReviewPopup();
            });
        }

        // 리뷰 업로드 관련 이벤트
        if (btnReviewUpload != null)
        {
            btnReviewUpload.onClick.AddListener(() =>
            {
                // 리뷰 텍스트를 안전하게 임시 저장
                if (reviewInputField != null)
                {
                    tempReviewText = reviewInputField.text.Trim();
                    Debug.Log($"리뷰 텍스트 임시 저장: {tempReviewText}");
                }

                reviewUploadPopup.SetActive(false);
                createReviewPopup.SetActive(false);
                CreateReview();
            });
        }

        if (btnReviewUploadCancel != null)
        {
            btnReviewUploadCancel.onClick.AddListener(() =>
            {
                reviewUploadPopup.SetActive(false);
                createReviewPopup.SetActive(true);
            });
        }
    }

    void RemoveEvent()
    {
        GlobalEvent.OnLawCourtPrepareTrial.Unsubscribe(ResetUI);

        // 리뷰 생성 관련 이벤트 제거
        reviewCreateButton.onClick.RemoveAllListeners();
        btnCreateReviewCancel.onClick.RemoveAllListeners();
        bntCreateReview.onClick.RemoveAllListeners();

        // 리뷰 목록 관련 이벤트 제거
        btnGetReviews.onClick.RemoveAllListeners();
        btnReviewListClose.onClick.RemoveAllListeners();

        // 리뷰 수정 관련 이벤트 제거
        if (btnEditReviewSave != null)
            btnEditReviewSave.onClick.RemoveAllListeners();
        if (btnEditReviewCancel != null)
            btnEditReviewCancel.onClick.RemoveAllListeners();
        if (btnShowEditReviewPopup != null)
            btnShowEditReviewPopup.onClick.RemoveAllListeners();

        // 리뷰 삭제 관련 이벤트 제거
        if (btnDeleteReview != null)
            btnDeleteReview.onClick.RemoveAllListeners();
        if (btnDeleteReviewCancel != null)
            btnDeleteReviewCancel.onClick.RemoveAllListeners();
        if (btnShowDeleteReviewPopup != null)
            btnShowDeleteReviewPopup.onClick.RemoveAllListeners();

        // 리뷰 업로드 관련 이벤트 제거
        if (btnReviewUpload != null)
            btnReviewUpload.onClick.RemoveAllListeners();
        if (btnReviewUploadCancel != null)
            btnReviewUploadCancel.onClick.RemoveAllListeners();
    }
    #endregion

    #region Review Creation
    /// <summary>
    /// 리뷰 업로드 팝업 표시
    /// </summary>
    public void ShowReviewUploadPopup()
    {
        if (reviewUploadPopup == null)
        {
            Debug.LogError("업로드 팝업 UI가 설정되지 않았습니다.");
            return;
        }

        // 후기 작성 팝업 닫기
        if (createReviewPopup != null)
            createReviewPopup.SetActive(false);

        // 업로드 팝업 표시
        reviewUploadPopup.SetActive(true);
        Debug.Log("리뷰 업로드 팝업 열림");
    }

    /// <summary>
    /// 리뷰 생성 함수
    /// </summary>
    public async void CreateReview()
    {
        try
        {
            // 임시 저장된 텍스트 사용 (없으면 reviewInputField에서 가져오기)
            string content = GetReviewContent();
            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("리뷰 내용을 입력해주세요.");
                return;
            }

            if (string.IsNullOrEmpty(lawCourtId))
            {
                Debug.LogError("법원 ID가 설정되지 않았습니다.");
                return;
            }

            if (PlayData.User == null)
            {
                Debug.LogError("로그인이 필요합니다.");
                return;
            }

            // 버튼 비활성화
            reviewCreateButton.interactable = false;
            Debug.Log("리뷰 생성 중...");

            networkManager.RPC_EnqueueReviewOperation(
              SMOE.LawCourt.LawCourtNetworkManager.ReviewOperationType.CREATE,
              lawCourtId,
              PlayData.User?.Id ?? "",
              "",
              content
          );

            CleanupAfterReviewCreation();

            // RestApi를 통해 리뷰 생성
            // var result = await RestApi.CreateReview(lawCourtId, PlayData.User.Id, content);

            // if (result != null)
            // {
            //     Debug.Log($"✅ 리뷰 생성 성공: {result.Id}");
            //     LogCreationTime(result.CreatedAt);
            //     CleanupAfterReviewCreation();

            //     // 네트워크를 통해 다른 클라이언트들에게 알림
            //     NotifyReviewCreated(result.Id);

            //     //LoadReviewList();
            // }
            // else
            // {
            //     Debug.LogError("❌ 리뷰 생성 실패");
            // }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"리뷰 생성 오류: {ex.Message}");
        }
        finally
        {
            if (reviewCreateButton != null)
                reviewCreateButton.interactable = true;
        }
    }
    #endregion

    #region Review List Management
    /// <summary>
    /// 법원 ID를 이용하여 리뷰 전체 목록 받아오기
    /// </summary>
    public async void LoadReviewList()
    {
        if (!reviewListPopup.activeSelf) return;
        
        ClearExistingReviewUI();
        try
        {
            if (string.IsNullOrEmpty(lawCourtId))
            {
                Debug.LogError("법원 ID가 설정되지 않았습니다.");
                return;
            }

            Debug.Log($"리뷰 목록 로딩 중... 법원 ID: {lawCourtId}");
            Debug.Log($"🔍 API 호출 전 - 법원 ID: '{lawCourtId}' (길이: {lawCourtId.Length})");

            // RestApi를 통해 리뷰 목록 가져오기 (역순으로)
            var reviews = await RestApi.GetReviews(lawCourtId);
            reviews.Reverse();

            LogReviewLoadResult(reviews);

            if (reviews != null && reviews.Count > 0)
            {
                Debug.Log($"✅ 리뷰 목록 로드 성공: {reviews.Count}개");

                reviewItems = ConvertToReviewItems(reviews);
                CreateReviewUIByReviewData(reviews);
                //LogReviewItems();
            }
            else
            {
                Debug.Log("📭 조회된 리뷰가 없습니다.");
                reviewItems.Clear();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"리뷰 목록 로드 오류: {ex.Message}");
            reviewItems.Clear();
        }
    }
    #endregion

    #region Review Editing
    /// <summary>
    /// 수정 팝업 위치 설정 및 표시
    /// </summary>
    public void ShowEditPopup(LawCourtReviewItem item)
    {
        Debug.Log($"🔄 ShowEditPopup 호출됨: reviewId={item?.reviewId}");

        if (item == null)
        {
            Debug.LogError("❌ ShowEditPopup: item이 null입니다!");
            return;
        }

        // btnEdit의 RectTransform 가져오기
        RectTransform btnEditRect = item.btnEdit.GetComponent<RectTransform>();
        RectTransform editPopupRect = editPopup.GetComponent<RectTransform>();

        // 두 UI 요소의 Canvas 찾기
        Canvas btnEditCanvas = btnEditRect.GetComponentInParent<Canvas>();
        Canvas editPopupCanvas = editPopupRect.GetComponentInParent<Canvas>();

        // 월드 좌표를 스크린 좌표로 변환 후 다시 로컬 좌표로 변환
        Vector3 worldPosition = btnEditRect.position;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(btnEditCanvas.worldCamera, worldPosition);

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            editPopupRect.parent as RectTransform,
            screenPosition,
            editPopupCanvas.worldCamera,
            out localPosition
        );

        editPopupRect.anchoredPosition = localPosition;
        

        // Queue 시스템용 - reviewId만 저장 (currentEditingItem은 UI 표시용으로만)
        currentEditingItem = item;
        currentEditingReviewId = item.reviewId;
        Debug.Log($"✅ 편집할 리뷰 설정: reviewId={currentEditingReviewId}");

        // 수정된 코드
        if (!item.isMyReview && item.isTeacher)
        {
            btnShowEditReviewPopup.interactable = false;
            //item.btnEdit.gameObject.SetActive(false);
            //btnEditReviewSave.gameObject.SetActive(false);  // 다른 사람 리뷰는 수정 불가
        }
        else
        {
            btnShowEditReviewPopup.interactable = true;
        }

        editPopup.SetActive(true);
        
    }

    /// <summary>
    /// 리뷰 수정 팝업 열기
    /// </summary>
    public void ShowEditReviewPopup()
    {
        if (editReviewPopup == null || editReviewInputField == null)
        {
            Debug.LogError("수정 팝업 UI가 설정되지 않았습니다.");
            return;
        }

        if (string.IsNullOrEmpty(currentEditingReviewId))
        {
            Debug.LogError("❌ 수정할 리뷰 ID가 설정되지 않았습니다. 다시 시도해주세요.");

            // 선택 팝업과 수정 팝업을 모두 닫기
            if (editPopup != null) editPopup.SetActive(false);
            if (editReviewPopup != null) editReviewPopup.SetActive(false);

            Debug.LogWarning("🔄 리뷰 수정을 위해 다시 수정 버튼을 눌러주세요.");
            return;
        }

        // 기존 내용 찾기 - 단순하게 currentEditingItem에서만
        if (currentEditingItem != null)
        {
            editReviewInputField.text = currentEditingItem.contents;
        }
        else
        {
            editReviewInputField.text = ""; // 찾지 못하면 빈 내용으로 시작
        }

        editReviewPopup.SetActive(true);
        Debug.Log($"✅ 리뷰 수정 팝업 열림: {currentEditingReviewId}");
    }

    /// <summary>
    /// UI에서 직접 리뷰 아이템 찾기 (더 이상 사용하지 않음 - Queue 시스템에서는 불필요)
    /// </summary>
    [System.Obsolete("Queue 시스템에서는 서버에서 처리하므로 로컬 검색이 불필요합니다.")]
    private LawCourtReviewItem FindReviewInUI(string reviewId)
    {
        if (reviewItemParent == null || string.IsNullOrEmpty(reviewId))
            return null;

        // reviewItemParent 하위의 모든 LawCourtReviewItem 컴포넌트 검색
        var allReviewItems = reviewItemParent.GetComponentsInChildren<LawCourtReviewItem>();

        foreach (var item in allReviewItems)
        {
            if (item != null && item.reviewId == reviewId)
            {
                Debug.Log($"🔍 UI에서 찾은 리뷰: {reviewId}");
                return item;
            }
        }

        Debug.LogWarning($"🔍 UI에서도 reviewId={reviewId}를 찾지 못했습니다.");
        return null;
    }

    /// <summary>
    /// 수정된 리뷰 저장
    /// </summary>
    public void SaveEditedReview()
    {
        try
        {
            Debug.Log($"🔄 SaveEditedReview 시작: reviewId={currentEditingReviewId}");

            // 간단한 검증만
            if (editReviewInputField == null)
            {
                Debug.LogError("❌ editReviewInputField가 null입니다.");
                return;
            }

            if (string.IsNullOrEmpty(currentEditingReviewId))
            {
                Debug.LogError("❌ currentEditingReviewId가 비어있습니다.");
                return;
            }

            string newContent = editReviewInputField.text.Trim();
            if (string.IsNullOrEmpty(newContent))
            {
                Debug.LogError("❌ 수정할 내용이 비어있습니다.");
                return;
            }

            string reviewId = currentEditingReviewId;

            Debug.Log($"🔄 수정 데이터: reviewId={reviewId}, content={newContent.Substring(0, Math.Min(20, newContent.Length))}...");

            // UI 즉시 닫기 및 초기화
            editReviewPopup.SetActive(false);
            currentEditingItem = null;
            currentEditingReviewId = "";

            Debug.Log($"🔄 UI 초기화 완료, Queue에 추가 시도...");

            // Queue에 수정 작업 추가 - 서버에서 모든 검증 처리
            if (networkManager == null)
            {
                Debug.LogError("❌ networkManager가 null입니다!");
                return;
            }

            if (string.IsNullOrEmpty(lawCourtId))
            {
                Debug.LogError("❌ lawCourtId가 비어있습니다!");
                return;
            }

            Debug.Log($"🔄 RPC_EnqueueReviewOperation 호출: reviewId={reviewId}");

            networkManager.RPC_EnqueueReviewOperation(
                SMOE.LawCourt.LawCourtNetworkManager.ReviewOperationType.UPDATE,
                lawCourtId,
                PlayData.User?.Id ?? "",
                reviewId,
                newContent
            );

            Debug.Log($"✅ Queue 추가 완료: {reviewId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ 리뷰 수정 오류: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 리뷰 수정 취소
    /// </summary>
    public void CancelEditReview()
    {
        if (editReviewPopup != null)
            editReviewPopup.SetActive(false);

        currentEditingItem = null;
        currentEditingReviewId = "";
        Debug.Log("리뷰 수정 취소됨");
    }
    #endregion

    #region Review Deletion
    /// <summary>
    /// 리뷰 삭제 확인 팝업 열기
    /// </summary>
    public void ShowDeleteReviewPopup()
    {
        if (deleteReviewPopup == null)
        {
            Debug.LogError("삭제 확인 팝업 UI가 설정되지 않았습니다.");
            return;
        }

        if (string.IsNullOrEmpty(currentEditingReviewId))
        {
            Debug.LogError("삭제할 리뷰 ID가 설정되지 않았습니다.");
            return;
        }

        deleteReviewPopup.SetActive(true);
        Debug.Log($"리뷰 삭제 확인 팝업 열림: {currentEditingReviewId}");
    }

    /// <summary>
    /// 리뷰 삭제 실행
    /// </summary>
    public void DeleteReview()
    {
        try
        {
            if (string.IsNullOrEmpty(currentEditingReviewId))
            {
                Debug.LogError("삭제할 리뷰 ID가 설정되지 않았습니다.");
                return;
            }

            string reviewId = currentEditingReviewId;

            // UI 즉시 닫기 및 초기화
            deleteReviewPopup.SetActive(false);
            currentEditingItem = null;
            currentEditingReviewId = "";

            Debug.Log($"🔄 리뷰 삭제 Queue에 추가: {reviewId}");

            // Queue에 삭제 작업 추가 - 서버에서 권한 검증 처리
            if (networkManager != null && !string.IsNullOrEmpty(lawCourtId))
            {
                networkManager.RPC_EnqueueReviewOperation(
                    SMOE.LawCourt.LawCourtNetworkManager.ReviewOperationType.DELETE,
                    lawCourtId,
                    PlayData.User?.Id ?? "",
                    reviewId,
                    ""
                );
            }
            else
            {
                Debug.LogError("❌ NetworkManager 또는 lawCourtId가 없습니다.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"리뷰 삭제 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 리뷰 삭제 취소
    /// </summary>
    public void CancelDeleteReview()
    {
        if (deleteReviewPopup != null)
            deleteReviewPopup.SetActive(false);

        currentEditingItem = null;
        currentEditingReviewId = "";
        Debug.Log("리뷰 삭제 취소됨");
    }
    #endregion

    #region UI Management
    /// <summary>
    /// 리뷰 데이터로 UI 생성
    /// </summary>
    private void CreateReviewUIByReviewData(List<LawCourtReview> reviews)
    {
        ClearExistingReviewUI();

        for (int i = 0; i < reviews.Count; i++)
        {
            var reviewData = reviews[i];
            var reviewUIObject = Instantiate(reviewItemPrefab, reviewItemParent);

            if (reviewUIObject != null)
            {
                reviewUIObject.gameObject.SetActive(true);
                reviewUIObject.manager = this;
                reviewUIObject.SetReviewData(reviewData);
            }
        }

        UpdateReviewCount(reviews.Count);
        Debug.Log($"🎉 총 {reviewItems.Count}개 리뷰 UI 생성 완료!");
    }

    /// <summary>
    /// 리뷰 개수 UI 업데이트
    /// </summary>
    private void UpdateReviewCount(int count)
    {
        txtReviewCount.text = $"재판 후기({count})";
    }

    /// <summary>
    /// 기존 리뷰 UI 아이템들 모두 삭제
    /// </summary>
    private void ClearExistingReviewUI()
    {
        if (reviewItemParent == null) return;

        int childCount = reviewItemParent.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = reviewItemParent.GetChild(i);
            if (child.gameObject.activeSelf)
                Destroy(child.gameObject);
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 리뷰 텍스트 내용 가져오기
    /// </summary>
    private string GetReviewContent()
    {
        if (!string.IsNullOrEmpty(tempReviewText))
        {
            Debug.Log("임시 저장된 리뷰 텍스트 사용");
            return tempReviewText;
        }
        else if (reviewInputField != null)
        {
            Debug.Log("reviewInputField에서 리뷰 텍스트 가져오기");
            return reviewInputField.text.Trim();
        }
        else
        {
            Debug.LogError("ReviewInputField가 설정되지 않았습니다.");
            return "";
        }
    }

    /// <summary>
    /// 리뷰 생성 후 정리 작업
    /// </summary>
    private void CleanupAfterReviewCreation()
    {
        if (reviewInputField != null)
            reviewInputField.text = "";

        tempReviewText = "";
        Debug.Log("임시 리뷰 텍스트 초기화 완료");

        if (createReviewPopup != null)
            createReviewPopup.SetActive(false);
    }

    /// <summary>
    /// 리뷰 수정 유효성 검사 (Queue 시스템에서는 더 이상 사용하지 않음)
    /// </summary>
    [System.Obsolete("Queue 시스템에서는 reviewId만으로 처리하므로 이 메서드는 더 이상 사용하지 않습니다.")]
    private bool ValidateEditReview()
    {
        if (currentEditingItem == null)
        {
            Debug.LogError("수정할 리뷰 아이템이 설정되지 않았습니다.");
            return false;
        }

        if (editReviewInputField == null)
        {
            Debug.LogError("수정 입력 필드가 설정되지 않았습니다.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 생성 시간 로그 출력
    /// </summary>
    private void LogCreationTime(DateTime createdAt)
    {
        Debug.Log($"🔍 Raw CreatedAt: {createdAt}");
        Debug.Log($"🔍 CreatedAt Ticks: {createdAt.Ticks}");

        if (createdAt == DateTime.MinValue || createdAt.Year < 2000)
        {
            Debug.LogWarning("⚠️ CreatedAt이 기본값입니다. 현재 시간으로 대체합니다.");
            DateTime currentTime = DateTime.Now;
            string timeText = GetRelativeTime(currentTime);
            Debug.Log($"생성 시간: {timeText} ({currentTime:yyyy.MM.dd HH:mm}) [현재 시간 사용]");
        }
        else
        {
            string timeText = GetRelativeTime(createdAt);
            Debug.Log($"생성 시간: {timeText} ({createdAt:yyyy.MM.dd HH:mm})");
        }
    }

    /// <summary>
    /// 리뷰 로드 결과 로그 출력
    /// </summary>
    private void LogReviewLoadResult(List<LawCourtReview> reviews)
    {
        Debug.Log($"🔍 API 호출 결과 분석:");
        Debug.Log($"   - reviews == null: {reviews == null}");
        if (reviews != null)
            Debug.Log($"   - reviews.Count: {reviews.Count}");
    }

    /// <summary>
    /// 리뷰 아이템 정보 로그 출력
    /// </summary>
    private void LogReviewItems()
    {
        Debug.Log($"📋 변환 완료: {reviewItems.Count}개 리뷰 아이템 생성");

        for (int i = 0; i < reviewItems.Count; i++)
        {
            var item = reviewItems[i];
            Debug.Log($"{i + 1}. {item.userName} ({item.date}): {item.contents.Substring(0, Math.Min(20, item.contents.Length))}...");
        }
    }

    /// <summary>
    /// LawCourtReview 목록을 LawCourtReviewItem 목록으로 변환
    /// </summary>
    private List<LawCourtReviewItem> ConvertToReviewItems(List<LawCourtReview> reviews)
    {
        List<LawCourtReviewItem> items = new List<LawCourtReviewItem>();

        foreach (var review in reviews)
        {
            LawCourtReviewItem item = new LawCourtReviewItem();

            item.userId = review.UserId;
            item.userName = GetUserNameFromId(review.UserId);
            item.contents = review.Content;
            item.date = FormatDateString(review.CreatedAt);
            item.isMyReview = IsMyReview(review.UserId);

            items.Add(item);

            Debug.Log($"🔄 변환됨: {item.userName} - {item.date} - 내 리뷰: {item.isMyReview}");
        }

        return items;
    }

    /// <summary>
    /// 사용자 ID로 사용자명 가져오기
    /// </summary>
    private string GetUserNameFromId(string userId)
    {
        if (PlayData.User != null && userId == PlayData.User.Id)
            return PlayData.User.Name ?? userId;

        return userId;
    }

    /// <summary>
    /// 내 리뷰인지 확인
    /// </summary>
    private bool IsMyReview(string userId)
    {
        return PlayData.User != null && userId == PlayData.User.Id;
    }

    /// <summary>
    /// 상대적 시간 표시 ("방금 전", "3분 전", "2시간 전", "3일 전")
    /// </summary>
    private string GetRelativeTime(DateTime dateTime)
    {
        TimeSpan timeDiff = DateTime.Now - dateTime;

        if (timeDiff.TotalSeconds < 60)
            return "방금 전";
        else if (timeDiff.TotalMinutes < 60)
            return $"{(int)timeDiff.TotalMinutes}분 전";
        else if (timeDiff.TotalHours < 24)
            return $"{(int)timeDiff.TotalHours}시간 전";
        else if (timeDiff.TotalDays < 7)
            return $"{(int)timeDiff.TotalDays}일 전";
        else
            return dateTime.ToString("yyyy.MM.dd HH:mm");
    }

    /// <summary>
    /// DateTime을 한국어 요일 포함 형식으로 변환 (2025.07.03.화 10:22)
    /// </summary>
    private string FormatDateString(DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue || dateTime.Year < 2000)
        {
            Debug.LogWarning("⚠️ DateTime이 기본값입니다. 현재 시간으로 대체합니다.");
            dateTime = DateTime.Now;
        }

        string[] koreanDayNames = { "일", "월", "화", "수", "목", "금", "토" };
        string dayOfWeek = koreanDayNames[(int)dateTime.DayOfWeek];

        return $"{dateTime.Year:0000}.{dateTime.Month:00}.{dateTime.Day:00}.{dayOfWeek} {dateTime.Hour:00}:{dateTime.Minute:00}";
    }

    /// <summary>
    /// Unix timestamp (밀리초)를 DateTime으로 변환 (추후 MongoDB 날짜 처리용)
    /// </summary>
    private DateTime ConvertFromUnixTimestamp(long unixTimeMilliseconds)
    {
        try
        {
            DateTimeOffset offset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds);
            return offset.DateTime;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Unix timestamp 변환 오류: {ex.Message}");
            return DateTime.Now;
        }
    }
    #endregion
}