using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using System;
using SMOE.LawCourt;
using System.Threading.Tasks;
using System.Linq;
using LawCourtModel = LawCourt;

namespace SMOE.LawCourt
{
    /// <summary>
    /// 법원 스크립트 컨트롤러와 리뷰 매니저의 네트워크 동기화를 담당하는 클래스
    /// </summary>
    public class LawCourtNetworkManager : NetworkBehaviour
    {
        public static LawCourtNetworkManager Instance { get; private set; }

        [Header("Network Synchronized Data")]
        [Networked] public int CurrentScriptIndex { get; set; } = 1;
        [Networked] public bool IsScriptCompleted { get; set; } = false;
        [Networked] public bool IsPlayingTTS { get; set; } = false;
        [Networked] public bool IsAutoMode { get; set; } = false;
        [Networked] public LawCourtRoleType CurrentRole { get; set; } = LawCourtRoleType.NONE;

        [Header("Review Manager Data")]
        [Networked] public bool IsReviewMode { get; set; } = false;
        [Networked] public int CurrentReviewIndex { get; set; } = 0;

        // Events for UI updates
        public event Action<int> OnScriptIndexChanged;
        public event Action<bool> OnTTSStateChanged;
        public event Action<bool> OnAutoModeChanged;
        public event Action<LawCourtRoleType> OnRoleChanged;
        public event Action<bool> OnReviewModeChanged;
        public event Action<int> OnReviewIndexChanged;
        public event Action<string, string, string> OnReviewListRefreshRequested;

        // Events for component management
        public event Action<string> OnCreateReviewManagerRequested;
        public event Action<string> OnCreateCourtScriptControllerRequested;
        public event Action<string> OnCreateAllCourtComponentsRequested;
        public event Action OnCourtScriptEnd;

        // References
        private CourtScriptController courtScriptController;

        private LawCourtReviewManager _reviewManager;
        private LawCourtTestManager _testManager;
        public LawCourtReviewManager ReviewManager
        {
            get
            {
                if (_reviewManager == null)
                {
                    _reviewManager = UIManager.Instance.MainCanvas.GetComponentInChildren<LawCourtReviewManager>();
                    _reviewManager.InstanceType = InstanceType.INTANTIATE;
                    Debug.Log($"ReviewManager 찾기 결과: {_reviewManager != null}");
                }
                return _reviewManager;
            }
        }

        public LawCourtTestManager TestManager
        {
            get
            {
                if (_testManager == null)
                {
                    _testManager = UIManager.Instance.MainCanvas.GetComponentInChildren<LawCourtTestManager>();
                    _testManager.InstanceType = InstanceType.INTANTIATE;
                    Debug.Log($"TestManager 찾기 결과: {_testManager != null}");
                }
                return _testManager;
            }
        }
        public LawCourtSessionManager sessionManager;

        [Header("Review Queue System")]
        private Queue<ReviewOperation> reviewOperationQueue = new Queue<ReviewOperation>();
        private bool isProcessingReviewQueue = false;

        // 리뷰 작업 타입 정의
        public enum ReviewOperationType
        {
            CREATE,
            UPDATE,
            DELETE,
            REFRESH_LIST
        }

        // 리뷰 작업 데이터 구조
        [System.Serializable]
        public struct ReviewOperation
        {
            public ReviewOperationType operationType;
            public string courtId;
            public string userId;
            public string reviewId;
            public string reviewData;
            public float timestamp;
        }



        public override void Spawned()
        {
            base.Spawned();

            // 재판 종료 이벤트 구독
            GlobalEvent.OnLawCourtTrialEnded.Subscribe(OnCourtEndedSetReviewMode);
            // 재판 다시 하기 이벤트 구독
            GlobalEvent.OnLawCourtPrepareTrial.Subscribe(OnLawCourtPrepareTrial);

            StartCoroutine(ProcessReviewQueue());

            if (Instance == null)
            {
                Instance = this;
                ////Debug.Log("LawCourtNetworkManager spawned and set as Instance");
            }
            else
            {
                Debug.LogWarning("Multiple LawCourtNetworkManager instances detected!");
            }

            // Find session manager
            sessionManager = FindFirstObjectByType<LawCourtSessionManager>();

            // Initialize script index and completion status
            if (Object.HasStateAuthority)
            {
                CurrentScriptIndex = 1; // Skip script 0
                IsScriptCompleted = false; // Initialize as not completed
            }

            // Video Chat Menu 이벤트 구독
            UIManager.Instance.VideoChatMenu.InitForLawCourt();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            StopAllCoroutines();

            // Video Chat Menu 이벤트 구독 해제
            UIManager.Instance.VideoChatMenu.DisposeForLawCourt();

            ReviewManager.Hide();
            TestManager.Hide();
            Debug.Log("ReviewManager 찾기 결과: " + ReviewManager != null);
            Debug.Log("TestManager 찾기 결과: " + TestManager != null);

            if (courtScriptController != null)
                courtScriptController.Hide();

            // 재판 종료 이벤트 구독 해제
            GlobalEvent.OnLawCourtTrialEnded.Unsubscribe(OnCourtEndedSetReviewMode);
            // 재판 다시 하기 이벤트 구독 해제
            GlobalEvent.OnLawCourtPrepareTrial.Unsubscribe(OnLawCourtPrepareTrial);

            if (Instance == this)
            {
                Instance = null;
            }

            base.Despawned(runner, hasState);
        }

        /// <summary>
        /// 테스트용: 리뷰 큐에 가짜 데이터 5개 추가
        /// </summary>
        [ContextMenu("테스트 - 리뷰 큐에 가짜 데이터 5개 추가")]
        private void TestAddFakeReviewsToQueue()
        {
            if (!Object.HasStateAuthority)
            {
                Debug.LogWarning("❌ StateAuthority가 아니므로 테스트 데이터 추가 불가");
                return;
            }

            Debug.Log("🧪 테스트: 리뷰 큐에 가짜 데이터 5개 추가 시작");

            for (int i = 1; i <= 5; i++)
            {
                var fakeOperation = new ReviewOperation
                {
                    operationType = ReviewOperationType.CREATE,
                    courtId = PlayData.Instance.CurrentLawCourt.Id, // 테스트용 고정값
                    userId = PlayData.User.Id,
                    reviewId = "",
                    reviewData = $"테스트 리뷰 내용 #{i} - 이것은 가짜 데이터입니다. 시간: {System.DateTime.Now:HH:mm:ss}",
                    timestamp = Time.time + i * 0.1f
                };

                reviewOperationQueue.Enqueue(fakeOperation);
                Debug.Log($"🧪 테스트 데이터 추가: #{i} - reviewId: {fakeOperation.reviewId}");
            }

            Debug.Log($"✅ 테스트 완료: 큐 크기 = {reviewOperationQueue.Count}");
        }

        /// <summary>
        /// CourtScriptController 참조 설정
        /// </summary>
        public void SetCourtScriptController(CourtScriptController controller)
        {
            courtScriptController = controller;
            ////Debug.Log("CourtScriptController reference set in LawCourtNetworkManager");
        }

        #region Script Control RPCs

        /// <summary>
        /// 다음 스크립트로 이동 요청
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestNextScript(string userId)
        {
            RPC_RequestNextScriptWithMode(userId, false);
        }

        /// <summary>
        /// 다음 스크립트로 이동 요청 (AutoMode 지원)
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public async void RPC_RequestNextScriptWithMode(string userId, bool isFromAutoMode)
        {
            Debug.Log($"🔥 RPC_RequestNextScriptWithMode called by user: {userId}, AutoMode: {isFromAutoMode}");

            if (!Object.HasStateAuthority) return;

            // 권한 체크 로직 추가
            var allScripts = CourtScriptManager.Instance?.GetAllScripts();
            if (allScripts == null || allScripts.Count == 0) return;

            // 마지막 스크립트 체크
            if (CurrentScriptIndex >= allScripts.Count - 1)
            {
                Debug.Log($"🔥 마지막 스크립트 도달: CurrentScriptIndex={CurrentScriptIndex}, 전체={allScripts.Count}");
                Debug.Log("마지막 스크립트에 도달했습니다.");

                //IsReviewMode = true;
                //RPC_NotifyReviewModeChanged(true);

                CurrentScriptIndex = 1;

                RPC_NotifyScriptEnd();
                return;
            }

            // 현재 스크립트 정보 가져오기
            // if (CurrentScriptIndex >= allScripts.Count) return;
            var currentScript = allScripts[CurrentScriptIndex];

            // 요청자의 역할 확인 (네트워크에서 userId로 역할 찾기)
            var requesterRole = await GetUserRoleFromUserId(userId);

            // AutoMode이거나 AI_NARRATION인 경우 권한 체크 우회
            if (!isFromAutoMode && !IsAutoMode && currentScript.Role != LawCourtRoleType.AI_NARRATION)
            {
                // 권한 체크
                if (!CanUserControlScript(currentScript, requesterRole, userId))
                {
                    Debug.Log($"🔴 권한 없음: {userId}의 역할 {requesterRole}는 {currentScript.Role} 스크립트를 제어할 수 없음");
                    return;
                }
            }
            else
            {
                Debug.Log($"🟡 권한 체크 우회: fromAutoMode={isFromAutoMode}, IsAutoMode={IsAutoMode}, AI_NARRATION={currentScript.Role == LawCourtRoleType.AI_NARRATION}");
            }

            Debug.Log($"🔥 카운팅 전: CurrentScriptIndex={CurrentScriptIndex}, 전체 스크립트={allScripts.Count}");

            // 현재 스크립트 정보 출력
            if (CurrentScriptIndex < allScripts.Count)
            {
                string dialoguePreview = currentScript.Dialogue?.Length > 20 ? currentScript.Dialogue.Substring(0, 20) + "..." : currentScript.Dialogue ?? "";
                Debug.Log($"🔥 현재 스크립트: [{CurrentScriptIndex}] {currentScript.Role} - {dialoguePreview}");
            }



            // 인덱스 증가
            CurrentScriptIndex++;
            //Debug.Log($"🔥 카운팅 후: CurrentScriptIndex={CurrentScriptIndex}");

            // 모든 클라이언트에게 알림
            RPC_NotifyScriptIndexChanged(CurrentScriptIndex);
        }

        /// <summary>
        /// 스크립트 인덱스 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyScriptIndexChanged(int newIndex)
        {
            //Debug.Log($"Script index changed to: {newIndex}");
            //if(HasStateAuthority)
            CurrentScriptIndex = newIndex;
            OnScriptIndexChanged?.Invoke(newIndex);
        }

        /// <summary>
        /// 스크립트 종료 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyScriptEnd()
        {
            Debug.Log("[LawCourtNetworkManager] 🚨 스크립트 종료");

            if (HasStateAuthority)
            {
                RPC_SetScriptCompletion(true);
            }

            OnCourtScriptEnd.Invoke();
            // UI에서 종료 처리를 할 수 있도록 이벤트 발생
        }

        /// <summary>
        /// TTS 재생 상태 변경
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetTTSState(bool isPlaying, string userId)
        {
            if (!Object.HasStateAuthority) return;

            IsPlayingTTS = isPlaying;
            RPC_NotifyTTSStateChanged(isPlaying);
        }

        /// <summary>
        /// TTS 상태 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyTTSStateChanged(bool isPlaying)
        {
            IsPlayingTTS = isPlaying;
            OnTTSStateChanged?.Invoke(isPlaying);
        }

        /// <summary>
        /// 오토모드 상태 변경
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetAutoMode(bool autoMode, string userId)
        {
            if (!Object.HasStateAuthority) return;

            // 이미 같은 상태인 경우 중복 처리 방지
            if (IsAutoMode == autoMode)
            {
                Debug.Log($"🟡 RPC_SetAutoMode: 이미 {autoMode} 상태임 - 중복 처리 방지");
                return;
            }

            Debug.Log($"🟡 RPC_SetAutoMode: {autoMode} by {userId}");
            IsAutoMode = autoMode;
            RPC_NotifyAutoModeChanged(autoMode);
        }

        /// <summary>
        /// 오토모드 상태 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyAutoModeChanged(bool autoMode)
        {
            IsAutoMode = autoMode;
            OnAutoModeChanged?.Invoke(autoMode);
        }

        /// <summary>
        /// 스크립트 완료 상태 변경
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetScriptCompletion(bool isCompleted)
        {
            if (!Object.HasStateAuthority) return;

            IsScriptCompleted = isCompleted;
        }

        #endregion

        #region Review Manager RPCs

        /// <summary>
        /// 리뷰 버튼 활성화
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_NotifyReviewButtonEnable(bool disabled)
        {

        }

        /// <summary>
        /// 리뷰 생성 알림 - Queue 시스템 사용
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_NotifyReviewCreated(string courtId, string userId, string reviewId)
        {
            RPC_EnqueueReviewOperation(ReviewOperationType.CREATE, courtId, userId, reviewId, "");
        }

        /// <summary>
        /// 리뷰 수정 알림 - Queue 시스템 사용
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_NotifyReviewUpdated(string courtId, string userId, string reviewId)
        {
            RPC_EnqueueReviewOperation(ReviewOperationType.UPDATE, courtId, userId, reviewId, "");
        }

        /// <summary>
        /// 리뷰 삭제 알림 - Queue 시스템 사용
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_NotifyReviewDeleted(string courtId, string userId, string reviewId)
        {
            RPC_EnqueueReviewOperation(ReviewOperationType.DELETE, courtId, userId, reviewId, "");
        }

        /// <summary>
        /// 리뷰 목록 새로고침 요청을 모든 클라이언트에게 전송
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_RequestReviewListRefresh(string courtId, string triggeredByUserId, string action)
        {
            //Debug.Log($"리뷰 목록 새로고침 요청: court={courtId}, action={action}, user={triggeredByUserId}");
            OnReviewListRefreshRequested?.Invoke(courtId, triggeredByUserId, action);
        }

        private void OnCourtEndedSetReviewMode()
        {
            if (!Object.HasStateAuthority) return;

            string userId = PlayData.User?.Id ?? "";
            RPC_SetReviewMode(true, userId);  // 재판 종료시 리뷰 모드 ON
        }

        /// <summary>
        /// 재판 다시 하기 시 모든 컴포넌트 재생성
        /// </summary>
        private void OnLawCourtPrepareTrial()
        {
            // 1. 네트워크 상태 초기화 (StateAuthority만)
            if (Object.HasStateAuthority)
            {
                CurrentScriptIndex = 1;
                string userId = PlayData.User?.Id ?? "";
                RPC_SetReviewMode(false, userId);
                RPC_SetScriptCompletion(false);

                // Queue 초기화
                reviewOperationQueue.Clear();
                isProcessingReviewQueue = false;
            }

            // 2. UI 컴포넌트 재생성 (모든 클라이언트)
            RecreateCourtComponents();
        }

        /// <summary>
        /// 법원 컴포넌트들 삭제 후 재생성
        /// </summary>
        private async void RecreateCourtComponents()
        {
            try
            {
                // 1. 기존 컴포넌트들 삭제
                courtScriptController.Hide();


                // 2. 삭제 완료를 위한 짧은 대기
                await System.Threading.Tasks.Task.Delay(100);


                Debug.Log("✅ 법원 컴포넌트들 재생성 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 법원 컴포넌트 재생성 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 리뷰 모드 설정
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetReviewMode(bool reviewMode, string userId)
        {
            if (!Object.HasStateAuthority) return;

            IsReviewMode = reviewMode;
            RPC_NotifyReviewModeChanged(reviewMode);
        }

        /// <summary>
        /// 리뷰 모드 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyReviewModeChanged(bool reviewMode)
        {
            IsReviewMode = reviewMode;
            OnReviewModeChanged?.Invoke(reviewMode);
        }

        /// <summary>
        /// 리뷰 인덱스 변경
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetReviewIndex(int reviewIndex, string userId)
        {
            if (!Object.HasStateAuthority) return;

            CurrentReviewIndex = reviewIndex;
            RPC_NotifyReviewIndexChanged(reviewIndex);
        }

        /// <summary>
        /// 리뷰 인덱스 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NotifyReviewIndexChanged(int reviewIndex)
        {
            CurrentReviewIndex = reviewIndex;
            OnReviewIndexChanged?.Invoke(reviewIndex);
        }

        #endregion

        #region Component Management RPCs

        /// <summary>
        /// 리뷰 매니저 생성 요청
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestCreateReviewManager(string userId)
        {
            if (!Object.HasStateAuthority) return;

            //Debug.Log($"RPC_RequestCreateReviewManager called by user: {userId}");

            // 모든 클라이언트에서 리뷰 매니저 생성
            RPC_CreateReviewManager(userId);
        }

        /// <summary>
        /// 모든 클라이언트에서 리뷰 매니저 생성
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_CreateReviewManager(string triggeredByUserId)
        {
            //Debug.Log($"RPC_CreateReviewManager: 리뷰 매니저 생성 by {triggeredByUserId}");
            OnCreateReviewManagerRequested?.Invoke(triggeredByUserId);
        }

        /// <summary>
        /// 법원 스크립트 컨트롤러 생성 요청
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestCreateCourtScriptController(string userId)
        {
            if (!Object.HasStateAuthority) return;

            //Debug.Log($"RPC_RequestCreateCourtScriptController called by user: {userId}");

            // 모든 클라이언트에서 법원 스크립트 컨트롤러 생성
            RPC_CreateCourtScriptController(userId);
        }



        /// <summary>
        /// 모든 클라이언트에서 법원 스크립트 컨트롤러 생성
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_CreateCourtScriptController(string triggeredByUserId)
        {
            //Debug.Log($"RPC_CreateCourtScriptController: 법원 스크립트 컨트롤러 생성 by {triggeredByUserId}");
            OnCreateCourtScriptControllerRequested?.Invoke(triggeredByUserId);
        }

        /// <summary>
        /// 모든 법원 컴포넌트 생성 요청
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestCreateAllCourtComponents(string userId)
        {
            if (!Object.HasStateAuthority) return;

            //Debug.Log($"RPC_RequestCreateAllCourtComponents called by user: {userId}");

            // 모든 클라이언트에서 법원 컴포넌트들 생성
            RPC_CreateAllCourtComponents(userId);
        }

        /// <summary>
        /// 모든 클라이언트에서 법원 컴포넌트들 생성
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_CreateAllCourtComponents(string triggeredByUserId)
        {
            //Debug.Log($"RPC_CreateAllCourtComponents: 모든 법원 컴포넌트 생성 by {triggeredByUserId}");
            OnCreateAllCourtComponentsRequested?.Invoke(triggeredByUserId);
        }

        #endregion

        #region Review Queue System

        /// <summary>
        /// 리뷰 작업을 Queue에 추가 (StateAuthority만)
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_EnqueueReviewOperation(ReviewOperationType opType, string courtId, string userId, string reviewId, string reviewData)
        {
            Debug.Log($"🔄 RPC_EnqueueReviewOperation 수신: opType={opType}, reviewId={reviewId}, HasStateAuthority={Object.HasStateAuthority}");

            if (!Object.HasStateAuthority)
            {
                Debug.LogWarning($"🔄 StateAuthority가 아니므로 Queue 처리 무시: {reviewId}");
                return;
            }

            Debug.Log($"🔄 Queue에 추가 중: {opType} - {reviewId}");

            var operation = new ReviewOperation
            {
                operationType = opType,
                courtId = courtId,
                userId = userId,
                reviewId = reviewId,
                reviewData = reviewData,
                timestamp = Time.time
            };

            reviewOperationQueue.Enqueue(operation);
            Debug.Log($"🔄 Queue에 추가: {opType} by {userId}, Queue 크기: {reviewOperationQueue.Count}");

            // Queue가 비어있었다면 즉시 처리 시작
            // if (!isProcessingReviewQueue)
            // {
            //     StartCoroutine(ProcessReviewQueue());
            // }
        }

        /// <summary>
        /// Queue의 리뷰 작업들을 순차적으로 처리
        /// </summary>
        private IEnumerator ProcessReviewQueue()
        {
            //
            Debug.Log($"🟢 Queue 처리 시작, 대기중인 작업: {reviewOperationQueue.Count}개");

            while (true)
            {
                //yield return null;
                yield return new WaitForSeconds(2f);
                if (Object.HasStateAuthority)
                {
                    while (reviewOperationQueue.Count > 0)
                    {
                        isProcessingReviewQueue = true;
                        var operation = reviewOperationQueue.Dequeue();
                        Debug.Log($"🔄 Queue 처리 중: {operation.operationType} by {operation.userId}");

                        // 각 작업 사이에 안전한 간격 두기
                        yield return new WaitForSeconds(0.1f);

                        // 작업 타입에 따라 처리
                        Debug.Log($"🔄 작업 실행: {operation.operationType} - {operation.reviewId}");

                        switch (operation.operationType)
                        {
                            case ReviewOperationType.CREATE:
                                Debug.Log($"🟢 CREATE 처리: {operation.reviewId}");
                                RPC_ProcessReviewCreate(operation.courtId, operation.userId, operation.reviewId, operation.reviewData);
                                break;
                            case ReviewOperationType.UPDATE:
                                Debug.Log($"🟡 UPDATE 처리: {operation.reviewId}");
                                RPC_ProcessReviewUpdate(operation.courtId, operation.userId, operation.reviewId, operation.reviewData);
                                break;
                            case ReviewOperationType.DELETE:
                                Debug.Log($"🔴 DELETE 처리: {operation.reviewId}");
                                RPC_ProcessReviewDelete(operation.courtId, operation.userId, operation.reviewId);
                                break;
                                // case ReviewOperationType.REFRESH_LIST:
                                //     Debug.Log($"🔄 REFRESH_LIST 처리");
                                //     RPC_ProcessReviewListRefresh(operation.courtId, operation.userId);
                                //break;
                        }

                        Debug.Log($"✅ 작업 실행 완료: {operation.operationType} - {operation.reviewId}");
                    }

                    // 각 작업 완료 후 추가 대기
                    if (isProcessingReviewQueue)
                    {
                        isProcessingReviewQueue = false;
                        yield return new WaitForSeconds(0.1f);
                        RPC_RequestReviewListRefresh(PlayData.Instance.CurrentLawCourt.Id, PlayData.User.Id, "");
                    }

                }

            }
            //isProcessingReviewQueue = false;

        }

        /// <summary>
        /// Queue에서 처리되는 안전한 리뷰 생성
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ProcessReviewCreate(string courtId, string userId, string reviewId, string reviewData)
        {
            Debug.Log($"🟢 Queue에서 처리: 리뷰 생성 {reviewId} by {userId}");
            //OnReviewListRefreshRequested?.Invoke(courtId, userId, "created");

            // ✅ StateAuthority에서만 실제 API 호출 (UPDATE/DELETE와 동일한 패턴)
            if (Object.HasStateAuthority)
            {
                Debug.Log($"🔄 StateAuthority에서 API 호출 시작: {reviewId}");
                _ = ProcessCreateReviewAsync(reviewId, reviewData, courtId, userId); // ← 이 메서드 추가 필요
            }
            else
            {
                Debug.Log($"🔄 Non-StateAuthority 클라이언트에서는 대기: {reviewId}");
                // StateAuthority에서 성공하면 별도 RPC로 새로고침 요청이 올 것임
            }

        }

        /// <summary>
        /// 리뷰 생성 비동기 처리 (API 호출)
        /// </summary>
        private async Task ProcessCreateReviewAsync(string reviewId, string reviewData, string courtId, string userId)
        {
            try
            {
                Debug.Log($"🔄 RestApi.CreateReview 호출 시작: reviewId={reviewId}, content={reviewData?.Substring(0, Math.Min(20, reviewData?.Length ?? 0))}...");

                // ⚠️ 여기서 실제 API 메서드 확인 필요
                var result = await RestApi.CreateReview(courtId, userId, reviewData); // ← 이 API 메서드가 존재하는지 확인 필요

                Debug.Log($"🔄 RestApi.CreateReview 결과: {(result != null ? "성공" : "실패")}");

                if (result != null)
                {
                    Debug.Log($"✅ Queue를 통한 리뷰 생성 성공: {reviewId}");

                    // ✅ RPC를 통해 모든 클라이언트에게 새로고침 요청
                    Debug.Log($"🔄 모든 클라이언트에게 새로고침 RPC 전송: courtId={courtId}, userId={userId}");
                    //RPC_RequestReviewListRefresh(courtId, userId, "created");
                }
                else
                {
                    Debug.LogError($"❌ Queue 리뷰 생성 실패: {reviewId} - API 결과가 null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Queue 리뷰 생성 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Queue에서 처리되는 안전한 리뷰 수정
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ProcessReviewUpdate(string courtId, string userId, string reviewId, string reviewData)
        {
            Debug.Log($"🟡 Queue에서 처리: 리뷰 수정 {reviewId} by {userId}");

            // StateAuthority에서만 실제 API 호출
            if (Object.HasStateAuthority)
            {
                Debug.Log($"🔄 StateAuthority에서 API 호출 시작: {reviewId}");
                _ = ProcessUpdateReviewAsync(reviewId, reviewData, courtId, userId);
            }
            else
            {
                Debug.Log($"🔄 Non-StateAuthority 클라이언트에서는 대기: {reviewId}");
                // StateAuthority에서 성공하면 별도 RPC로 새로고침 요청이 올 것임
            }
        }

        /// <summary>
        /// 리뷰 수정 비동기 처리 (API 호출)
        /// </summary>
        private async Task ProcessUpdateReviewAsync(string reviewId, string newContent, string courtId, string userId)
        {
            try
            {
                Debug.Log($"🔄 RestApi.UpdateReview 호출 시작: reviewId={reviewId}, content={newContent?.Substring(0, Math.Min(20, newContent?.Length ?? 0))}...");

                var result = await RestApi.UpdateReview(reviewId, newContent);

                Debug.Log($"🔄 RestApi.UpdateReview 결과: {(result != null ? "성공" : "실패")}");

                if (result != null)
                {
                    Debug.Log($"✅ Queue를 통한 리뷰 수정 성공: {reviewId}");

                    // ✅ RPC를 통해 모든 클라이언트에게 새로고침 요청
                    Debug.Log($"🔄 모든 클라이언트에게 새로고침 RPC 전송: courtId={courtId}, userId={userId}");
                    //RPC_RequestReviewListRefresh(courtId, userId, "updated");
                }
                else
                {
                    Debug.LogError($"❌ Queue 리뷰 수정 실패: {reviewId} - API 결과가 null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Queue 리뷰 수정 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Queue에서 처리되는 안전한 리뷰 삭제
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ProcessReviewDelete(string courtId, string userId, string reviewId)
        {
            Debug.Log($"🔴 Queue에서 처리: 리뷰 삭제 {reviewId} by {userId}");

            // StateAuthority에서만 실제 API 호출
            if (Object.HasStateAuthority)
            {
                _ = ProcessDeleteReviewAsync(reviewId, courtId, userId);
            }
            else
            {
                // 다른 클라이언트들은 바로 UI 새로고침
                //OnReviewListRefreshRequested?.Invoke(courtId, userId, "deleted");
            }
        }

        /// <summary>
        /// 리뷰 삭제 비동기 처리 (API 호출)
        /// </summary>
        private async Task ProcessDeleteReviewAsync(string reviewId, string courtId, string userId)
        {
            try
            {
                var result = await RestApi.DeleteReview(reviewId);

                if (result != null)
                {
                    Debug.Log($"✅ Queue를 통한 리뷰 삭제 성공: {reviewId}");
                    // 성공 시 모든 클라이언트에게 새로고침 요청
                    //RPC_RequestReviewListRefresh(courtId, userId, "deleted");
                }
                else
                {
                    Debug.LogError($"❌ Queue 리뷰 삭제 실패: {reviewId}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Queue 리뷰 삭제 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Queue에서 처리되는 안전한 리스트 새로고침
        /// </summary>
        // [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        // private void RPC_ProcessReviewListRefresh(string courtId, string userId)
        // {
        //     Debug.Log($"🔄 Queue에서 처리: 리뷰 목록 새로고침 by {userId}");
        //     OnReviewListRefreshRequested?.Invoke(courtId, userId, "refresh");
        // }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 현재 사용자가 현재 스크립트에 대한 권한이 있는지 확인
        /// </summary>
        public bool CanUserControlCurrentScript()
        {
            if (CourtScriptManager.Instance == null)
            {
                Debug.Log($"🔴 CanUserControlCurrentScript: CourtScriptManager.Instance가 null");
                return false;
            }

            var allScripts = CourtScriptManager.Instance.GetAllScripts();
            if (allScripts == null || CurrentScriptIndex >= allScripts.Count)
            {
                Debug.Log($"🔴 CanUserControlCurrentScript: 스크립트 범위 오류 (인덱스={CurrentScriptIndex}, 전체={allScripts?.Count})");
                return false;
            }

            var currentScript = allScripts[CurrentScriptIndex];
            var userRole = PlayData.Instance?.GetLawCourtRole() ?? LawCourtRoleType.NONE;
            var userId = PlayData.User?.Id ?? "";

            Debug.Log($"🔍 CanUserControlCurrentScript: 사용자={userId}, 사용자역할={userRole}, 스크립트역할={currentScript.Role}, 인덱스={CurrentScriptIndex}");
            return CanUserControlScript(currentScript, userRole, userId);
        }

        /// <summary>
        /// 특정 사용자가 특정 스크립트를 제어할 수 있는지 확인
        /// </summary>
        private bool CanUserControlScript(CourtScript script, LawCourtRoleType userRole, string userId)
        {
            if (script == null) return false;

            // AI_NARRATION인 경우 자동 진행
            if (script.Role == LawCourtRoleType.AI_NARRATION)
            {
                Debug.Log($"🔴 권한 체크: AI_NARRATION은 자동 진행 (권한 없음)");
                return false; // 자동 진행되므로 사용자가 제어할 수 없음
            }

            // 사용자 역할이 없는 경우
            if (userRole == LawCourtRoleType.NONE)
            {
                Debug.Log($"🔴 권한 체크 실패: 사용자 역할이 없음 ({userId})");
                return false;
            }

            // sessionManager 참조 가져오기
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<LawCourtSessionManager>();
            }

            // 역할이 없는 사람이 있으면 AutoMode
            if (sessionManager != null)
            {
                var roleCount = sessionManager.GetRoleCount(script.Role);
                if (roleCount <= 0)
                {
                    Debug.Log($"🔴 권한 체크: 해당 역할 플레이어 없음 ({script.Role}) → AutoMode");
                    return false; // AutoMode로 처리
                }
            }

            // 사용자 역할과 현재 스크립트 역할이 일치하는지 확인
            bool hasPermission = userRole == script.Role;
            Debug.Log($"🔵 권한 체크: 사용자 역할={userRole}, 스크립트 역할={script.Role}, 권한={hasPermission} ({userId})");
            return hasPermission;
        }

        /// <summary>
        /// 사용자 ID로부터 역할을 가져오기
        /// </summary>
        private async Task<LawCourtRoleType> GetUserRoleFromUserId(string userId)
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<LawCourtSessionManager>();
            }

            if (sessionManager != null)
            {
                // 세션 매니저에서 사용자 역할 찾기
                var userRole = sessionManager.GetUserRole(userId);
                Debug.Log($"🟡 GetUserRoleFromUserId: sessionManager에서 {userId} → {userRole}");
                if (userRole != LawCourtRoleType.NONE)
                {
                    return userRole;
                }
                else if (userRole == LawCourtRoleType.NONE)
                {
                    var curCourtId = PlayData.Instance.CurrentLawCourt.Id;
                    LawCourtModel courtData = await RestApi.GetCourt(curCourtId);
                    var user = courtData.Users.FirstOrDefault(x => x.Id == userId);
                    if (user != null)
                    {
                        var noneUser = PlayData.Instance.CurrentLawCourt.Users.FirstOrDefault(x => x.Id == userId);
                        noneUser.Role = user.Role;
                        return user.Role;
                    }
                }
            }

            // 로컬 사용자인 경우 PlayData에서 가져오기
            if (userId == PlayData.User?.Id)
            {
                var localRole = PlayData.Instance?.GetLawCourtRole();
                Debug.Log($"🟡 GetUserRoleFromUserId: PlayData에서 {userId} → {localRole}");
                return localRole ?? LawCourtRoleType.NONE;
            }

            Debug.LogWarning($"🔴 사용자 역할을 찾을 수 없음: {userId}");
            return LawCourtRoleType.NONE;
        }

        /// <summary>
        /// 스크립트 완료 상태 확인
        /// </summary>
        /// <returns>스크립트가 완료되었으면 true, 아니면 false</returns>
        public bool GetScriptCompletionStatus()
        {
            return IsScriptCompleted;
        }

        /// <summary>
        /// 네트워크 동기화된 데이터를 로컬 UI에 적용
        /// </summary>
        public void SyncToLocalUI()
        {
            OnScriptIndexChanged?.Invoke(CurrentScriptIndex);
            OnTTSStateChanged?.Invoke(IsPlayingTTS);
            OnAutoModeChanged?.Invoke(IsAutoMode);
            OnReviewModeChanged?.Invoke(IsReviewMode);
            OnReviewIndexChanged?.Invoke(CurrentReviewIndex);
        }

        #endregion
    }
}