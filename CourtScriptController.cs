using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
//using UnityEngine.UIElements;
using System.Threading.Tasks;



namespace SMOE.LawCourt
{
    public class CourtScriptController : UIBase
    {
        [Header("UI References")]
        public TextMeshProUGUI dialogueText;
        public TextMeshProUGUI roleText;
        public TextMeshProUGUI stepText;
        public TextMeshProUGUI nextStepBtnText;
        public Button nextStep;

        [Header("Component References")]
        [SerializeField] private TTSPlayer ttsPlayer;

        private List<CourtScript> allScripts;
        private int currentIndex = 0;
        private CourtScript currentScript;
        private bool isPlayingTTS = false;
        private bool isAutoMode = false;  // AutoMode 상태 추적

        [Header("Badge Objects")]
        public List<LowCourtBadge> badgeList;


        [Header("UI Size")]
        public Button btnLargeView;
        public Button btnSmallView;
        public ScrollRect scrollRect;
        public RectTransform bgRT;
        public RectTransform contentsRT;
        public RectTransform bottomAreaRT;

        LawCourtSessionManager sessionManager;
        LawCourtNetworkManager networkManager;

        public GameObject alert;
        public TextMeshProUGUI alertText;

        public GameObject courtStartAlertAnim;
        public GameObject scriptBox;
        public RectTransform rt_scriptBox;

        PresentaionManager presentaionManager;

        /* UI 사이즈 조절
        large
        bg: 322
        contents : 274
        BottomArea : 230

        small
        bg : 222
        contents : 174
        bottomArea : 130
        */

        //public GameObject courtEndAlertAnim;

        public static async Task<CourtScriptController> CreateAsync()
        {
            var prefab = await AddressablesHelper.LoadPrefabAsync<CourtScriptController>("Assets/Prefabs/UI/LawCourt/LawCourtScriptController.prefab");

            if (prefab == null) return null;

            var canvas = UIManager.Instance.MainCanvas.transform;
            var courtScript = Instantiate(prefab, canvas);
            courtScript.name = "LowCourtScriptController";
            courtScript.InitRect(AnchorMode.Stretch);

            return courtScript;
        }



        public static void Destroy()
        {
            var canvas = UIManager.Instance.MainCanvas;
            var courtScript = canvas.GetComponentInChildren<CourtScriptController>();
            if (courtScript != null)
            {
                courtScript.DestroyInternal();
            }
        }

        private void DestroyInternal()
        {
            Hide();
        }

        protected override void OnDestroy()
        {
            // 네트워크 이벤트 구독 해제
            UnsubscribeFromNetworkEvents();

            // 재판 종료 이벤트 구독 해제
            GlobalEvent.OnLawCourtTrialEnded.Unsubscribe(CourtEnd);

            // 이벤트 리스너 정리
            if (nextStep != null)
            {
                nextStep.onClick.RemoveListener(OnNextStepClicked);
            }

            if (btnLargeView != null)
            {
                btnLargeView.onClick.RemoveListener(OnLargeViewClicked);
            }

            if (btnSmallView != null)
            {
                btnSmallView.onClick.RemoveListener(OnSmallViewClicked);
            }

            base.OnDestroy();
        }

        protected override void Start()
        {
            rt_scriptBox = scriptBox.GetComponent<RectTransform>();
            sessionManager = FindFirstObjectByType<LawCourtSessionManager>();
            networkManager = FindFirstObjectByType<LawCourtNetworkManager>();

            // UIBase 설정 (맨 앞에 추가)
            type = UIType.FULLSCREEN;
            InstanceType = InstanceType.INTANTIATE;

            // nextStep 버튼 이벤트 연결
            if (nextStep != null)
            {
                nextStep.onClick.AddListener(OnNextStepClicked);
            }

            // UI 크기 조절 버튼 이벤트 연결
            if (btnLargeView != null)
            {
                btnLargeView.onClick.AddListener(OnLargeViewClicked);
            }

            if (btnSmallView != null)
            {
                btnSmallView.onClick.AddListener(OnSmallViewClicked);
            }

            // TTSPlayer 찾기
            if (ttsPlayer == null)
            {
                ttsPlayer = TTSPlayer.Instance;
            }

            // 재판 종료 이벤트 구독
            GlobalEvent.OnLawCourtTrialEnded.Subscribe(CourtEnd);

            // 네트워크 매니저 연동
            StartCoroutine(WaitForNetworkManagerAndInitialize());
        }

        void CourtEnd()
        {
            scriptBox.SetActive(false);
            ttsPlayer.Stop();
            UpdateAlert(false);
            //courtEndAlertAnim.SetActive(true);

            // 초기화 처리
            dialogueText.text = "";
            stepText.text = "";
            StopAllCoroutines();
        }

        private IEnumerator WaitForNetworkManagerAndInitialize()
        {
            // 네트워크 매니저가 준비될 때까지 대기
            yield return new WaitUntil(() =>
                networkManager != null &&
                CourtScriptManager.Instance != null &&
                CourtScriptManager.Instance.IsDataLoaded
            );

            // 네트워크 매니저에 자신을 등록
            networkManager.SetCourtScriptController(this);

            // 네트워크 이벤트 구독
            SubscribeToNetworkEvents();

            var currentUserId = PlayData.User.Id;
            // 유저 역할 none 일때 서버에서 역할 가져오기
            if (networkManager.sessionManager.GetUserRole(currentUserId) == LawCourtRoleType.NONE)
            {
                var curCourtId = PlayData.Instance.CurrentLawCourt.Id;
                var task = RestApi.GetCourt(curCourtId);
                yield return new WaitUntil(() => task.IsCompleted);

                if (task.Result != null)
                {
                    var user = task.Result.Users.FirstOrDefault(x => x.Id == currentUserId);
                    var noneUser = PlayData.Instance.CurrentLawCourt.Users.FirstOrDefault(x => x.Id == currentUserId);
                    noneUser.Role = user.Role;
                }
            }


            // SHOW ALERT UI
            if (courtStartAlertAnim != null)
            {
                courtStartAlertAnim.SetActive(true);
                yield return new WaitForSeconds(3f);
                courtStartAlertAnim.SetActive(false);
            }

            // 스크립트 박스 활성화
            if (scriptBox != null)
            {
                scriptBox.SetActive(true);
            }

            // 초기 스크립트 로드
            LoadAllScripts();
            LoadCurrentScript();

            // 네트워크 상태를 로컬 UI에 동기화
            networkManager.SyncToLocalUI();

            // alert 표시
            UpdateAlert(true);
            yield return new WaitForSeconds(3f);
            UpdateAlert(false);

        }

        void UpdateAlert(bool value)
        {
            alert.SetActive(value);
            if (value)
            {
                alertText.text = isUserPlayer() ? "재판이 시작됐어요. 순서에 맞춰 대본을 읽어주세요." :
                    "재판을 시작합니다. 진행되는 재판을 방청해 주세요.";
            }
            else alertText.text = "";
        }

        bool isUserPlayer()
        {
            var role = PlayData.Instance.GetLawCourtRole();
            return role != LawCourtRoleType.AUDIENCE;
        }

        private void SubscribeToNetworkEvents()
        {
            if (networkManager == null) return;

            networkManager.OnScriptIndexChanged += OnNetworkScriptIndexChanged;
            networkManager.OnTTSStateChanged += OnNetworkTTSStateChanged;
            networkManager.OnAutoModeChanged += OnNetworkAutoModeChanged;
            networkManager.OnCourtScriptEnd += OnCourtScriptEnd;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (networkManager == null) return;

            networkManager.OnScriptIndexChanged -= OnNetworkScriptIndexChanged;
            networkManager.OnTTSStateChanged -= OnNetworkTTSStateChanged;
            networkManager.OnAutoModeChanged -= OnNetworkAutoModeChanged;
            networkManager.OnCourtScriptEnd -= OnCourtScriptEnd;
        }

        private void OnNetworkScriptIndexChanged(int newIndex)
        {
            Debug.Log($"🔷 OnNetworkScriptIndexChanged: newIndex={newIndex}, 전체스크립트={allScripts?.Count}");
            currentIndex = newIndex;
            LoadCurrentScript();

            // ⭐ 마지막 스크립트 체크
            bool isLastScript = allScripts != null && currentIndex >= allScripts.Count - 1;
            if (isLastScript)
            {
                Debug.Log($"🔴 마지막 스크립트 로드됨: 버튼 비활성화");
                SetNextStepButtonActive(false);
                return;
            }

            // AI_NARRATION인 경우 모든 클라이언트에서 TTS 재생
            if (currentScript != null && currentScript.Role == LawCourtRoleType.AI_NARRATION)
            {
                Debug.Log($"🟡 AI_NARRATION 감지: 모든 클라이언트에서 재생, 대사 길이={currentScript.Dialogue?.Length ?? 0}");
                StartCoroutine(DelayedPlayTTSAndAutoNext());
            }
        }

        private IEnumerator DelayedPlayTTSAndAutoNext()
        {
            // AI_NARRATION일 때 즉시 버튼 비활성화 (잠깐 활성화 방지)
            SetNextStepButtonActive(false);

            // UI 업데이트 완료를 위한 짧은 대기
            yield return new WaitForSeconds(0.1f);

            // TTS 재생 시작
            StartCoroutine(PlayTTSAndAutoNext());
        }

        // return current role
        public LawCourtRoleType GetCurrentRole()
        {
            return currentScript.Role;
        }


        private void OnNetworkTTSStateChanged(bool isPlaying)
        {
            isPlayingTTS = isPlaying;

            // TTS 상태에 따른 UI 업데이트
            if (!isPlaying && !isAutoMode)
            {
                // 권한 체크 추가
                bool hasPermission = networkManager != null && networkManager.CanUserControlCurrentScript();
                SetNextStepButtonActive(hasPermission);
            }
        }

        private void OnNetworkAutoModeChanged(bool autoMode)
        {
            isAutoMode = autoMode;

            if (autoMode)
            {
                EnableBadge(LawCourtRoleType.AUTO_MODE);

                var autoModeBadge = badgeList.FirstOrDefault(b => b.roleType == LawCourtRoleType.AUTO_MODE) as LowCourtBadgeAutoMode;
                autoModeBadge.SetAutoModeBadge(currentScript.Role);
                Debug.Log($"🔵 AutoMode 배지 업데이트: {currentScript.Role}");

                SetNextStepButtonActive(false);

                // StateAuthority만 타이머를 시작
                if (networkManager != null && networkManager.Object.HasStateAuthority)
                {
                    Debug.Log($"🟡 StateAuthority에서 AutoMode 타이머 시작");
                    StartCoroutine(AutoMode());
                }
                else
                {
                    Debug.Log($"🟡 다른 클라이언트: AutoMode UI만 업데이트");
                }
            }
            else
            {
                // AutoMode 종료 시 권한 체크하여 버튼 활성화
                if (networkManager != null)
                {
                    bool hasPermission = networkManager.CanUserControlCurrentScript();
                    SetNextStepButtonActive(hasPermission);
                }
                else
                {
                    SetNextStepButtonActive(false);
                }
            }
        }
        void EnableBadge(LawCourtRoleType role)
        {
            foreach (var badge in badgeList)
                badge.gameObject.SetActive(badge.roleType == role);
        }

        private IEnumerator WaitForDataAndInitialize()
        {
            // 데이터 로딩 완료까지 대기
            yield return new WaitUntil(() => CourtScriptManager.Instance != null && CourtScriptManager.Instance.IsDataLoaded);

            LoadAllScripts();
            LoadCurrentScript();
        }

        /// <summary>
        /// 모든 스크립트 데이터 로드
        /// </summary>
        private void LoadAllScripts()
        {
            if (CourtScriptManager.Instance != null)
            {
                allScripts = CourtScriptManager.Instance.GetAllScripts();
                currentIndex = 1; // 0번 스크립트는 무시
            }
        }

        /// <summary>
        /// nextStep 버튼 클릭 이벤트
        /// </summary>
        public void OnNextStepClicked()
        {
            if (isPlayingTTS) return;

            // ⭐ 마지막 스크립트에서는 클릭 무시
            bool isLastScript = allScripts != null && currentIndex >= allScripts.Count - 1;
            if (isLastScript)
            {
                Debug.Log($"🔴 마지막 스크립트에서 클릭 무시: 현재인덱스={currentIndex}, 전체={allScripts?.Count}");
                return;
            }

            // 권한 체크 추가 (AutoMode가 아닌 경우만)
            if (!isAutoMode && networkManager != null && !networkManager.CanUserControlCurrentScript())
            {
                Debug.Log($"🔴 권한 없음: 클릭 무시 (현재 역할={PlayData.Instance?.GetLawCourtRole()}, 스크립트 역할={currentScript?.Role})");
                return;
            }

            string triggerSource = isAutoMode ? "AutoMode" : "Button";
            Debug.Log($"🔵 OnNextStepClicked 호출됨: 트리거={triggerSource}, 현재인덱스={currentIndex}");

            // 네트워크 매니저를 통해 다음 스크립트 요청
            if (networkManager != null)
            {
                string userId = PlayData.User?.Id ?? "";
                networkManager.RPC_RequestNextScript(userId);
            }
            else
            {
                // 네트워크 매니저가 없을 때는 로컬 처리 (백워드 호환성)
                NextScript();
                LoadCurrentScript();

                // AI_NARRATION인 경우 자동 TTS 재생
                if (currentScript != null && currentScript.Role == LawCourtRoleType.AI_NARRATION)
                {
                    StartCoroutine(PlayTTSAndAutoNext());
                }
            }
        }

        /// <summary>
        /// 다음 스크립트로 이동
        /// </summary>
        private void NextScript()
        {
            if (allScripts == null || allScripts.Count == 0) return;

            // 마지막 스크립트 체크
            if (currentIndex >= allScripts.Count - 1)
            {

                return;
            }

            currentIndex++;
        }

        private void OnCourtScriptEnd()
        {
            // 마지막 스크립트에 도달했을 때의 처리
            Debug.Log("🔴 OnCourtScriptEnd 호출됨: 마지막 스크립트에 도달했습니다.");
            SetNextStepButtonActive(false);
            //SetScriptBoxPosition(70);
        }

        void SetScriptBoxPosition(float y)
        {
            rt_scriptBox.anchoredPosition = new Vector2(0, y);
        }

        /// <summary>
        /// TTS 재생 후 자동으로 다음 스텝으로 진행
        /// </summary>
        private IEnumerator PlayTTSAndAutoNext()
        {
            if (currentScript == null) yield break;

            // AI_NARRATION이 아닌 경우 자동 진행 방지
            if (currentScript.Role != LawCourtRoleType.AI_NARRATION)
            {
                Debug.Log($"🔴 자동 진행 방지: AI_NARRATION이 아닌 스크립트 ({currentScript.Role})");
                yield break;
            }

            Debug.Log($"🟡 TTS 자동 재생 시작: {currentScript.Role}");

            // 버튼 비활성화
            SetNextStepButtonActive(false);
            isPlayingTTS = true;

            // TTS 재생 시작
            if (networkManager != null)
            {
                networkManager.RPC_SetTTSState(true, PlayData.User.Id);
            }

            // TTS 재생
            if (ttsPlayer != null)
            {
                if (presentaionManager == null)
                {
                    presentaionManager = FindFirstObjectByType<PresentaionManager>();
                }

                presentaionManager.MuteAllSpeakers();

                ttsPlayer.PlayText(currentScript.Dialogue);

                // TTS 재생 완료까지 대기
                while (ttsPlayer.IsPlaying())
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }

            presentaionManager.UnmuteOnlySpeakers();

            // 잠시 대기 후 다음 스텝으로 자동 진행
            yield return new WaitForSeconds(0.5f);

            // TTS 재생 종료
            if (networkManager != null)
            {
                networkManager.RPC_SetTTSState(false, PlayData.User.Id);
            }

            // ⭐ StateAuthority만 다음 스크립트로 진행
            if (networkManager != null && networkManager.Object.HasStateAuthority)
            {
                string userId = PlayData.User?.Id ?? "";
                Debug.Log($"🔥 AI_NARRATION TTS 완료 후 카운팅 요청: 현재인덱스={currentIndex}, userId={userId}");
                networkManager.RPC_RequestNextScript(userId);
            }
            else if (networkManager != null)
            {
                Debug.Log($"🟡 클라이언트: TTS 완료, StateAuthority 진행 신호 대기");
            }
            else
            {
                // 네트워크 매니저가 없을 때만 로컬 처리 (백워드 호환성)
                NextScript();
                LoadCurrentScript();
            }

            // 버튼 다시 활성화 (AutoMode가 아닐 때만)
            isPlayingTTS = false;
            if (!isAutoMode)
            {
                // 권한이 있는 사용자만 버튼 활성화
                bool hasPermission = networkManager != null && networkManager.CanUserControlCurrentScript();
                SetNextStepButtonActive(hasPermission);
            }

            // ⚠️ 연속 재생 로직은 OnNetworkScriptIndexChanged에서 처리됨
            // (네트워크 동기화로 인해 자동으로 다음 AI_NARRATION 실행됨)
        }

        /// <summary>
        /// 현재 스크립트 정보를 로드하여 UI에 표시
        /// </summary>
        private void LoadCurrentScript()
        {
            if (allScripts == null || allScripts.Count == 0 || currentIndex >= allScripts.Count)
            {
                currentScript = null;
                return;
            }

            currentScript = allScripts[currentIndex];
            UpdateUI();
        }


        /// <summary>
        /// 마침표, 느낌표, 물음표 후에 줄바꿈 추가
        /// </summary>
        private string FormatDialogueWithLineBreaks(string dialogue)
        {
            if (string.IsNullOrEmpty(dialogue)) return dialogue;

            // 정규식: 마침표, 느낌표, 물음표 뒤에 공백이 있고 다음 문자가 있으면 줄바꿈 추가
            string pattern = @"([.!?])\s+(?=\S)";
            string replacement = "$1\n";

            return System.Text.RegularExpressions.Regex.Replace(dialogue, pattern, replacement);
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (currentScript == null) return;

            // 단계 텍스트 업데이트
            if (stepText != null)
            {
                stepText.text = $"단계: {currentScript.Step}";
            }

            // 역할 텍스트 업데이트
            if (roleText != null)
            {
                roleText.text = GetRoleDisplayName(currentScript.Role);
            }

            // 대화 텍스트 업데이트
            if (dialogueText != null)
            {
                string formattedText = FormatDialogueWithLineBreaks(currentScript.Dialogue);
                dialogueText.text = formattedText;
            }

            // 뱃지 활성화
            EnableBadge(currentScript.Role);

            // ⭐ 마지막 스크립트 체크
            bool isLastScript = allScripts != null && currentIndex >= allScripts.Count - 1;
            if (isLastScript)
            {
                Debug.Log($"🔴 마지막 스크립트 도달: 버튼 비활성화 (현재인덱스={currentIndex}, 전체={allScripts?.Count})");
                SetNextStepButtonActive(false);
                if (networkManager != null && networkManager.Object.HasStateAuthority)
                {
                    networkManager.RPC_RequestNextScript(PlayData.User?.Id ?? "");
                }
                return;
            }

            // AI_NARRATION인 경우 자동 진행 (권한 체크 없음)
            if (currentScript.Role == LawCourtRoleType.AI_NARRATION)
            {
                Debug.Log($"🟡 AI_NARRATION 감지: 자동 진행 예정");
                SetNextStepButtonActive(false); // AI_NARRATION은 버튼 비활성화
                return; // AI_NARRATION은 OnNetworkScriptIndexChanged에서 자동 처리됨
            }

            // 조건문 구조 수정
            if (currentScript.Role != LawCourtRoleType.AI_NARRATION)
            {
                var roleCount = sessionManager.GetRoleCount(currentScript.Role);
                Debug.Log($"🟠 역할 체크: {currentScript.Role} 역할 플레이어 수 = {roleCount}");
                if (roleCount <= 0)
                {
                    // 이미 AutoMode가 활성화된 경우 중복 호출 방지
                    if (!isAutoMode && (networkManager == null || !networkManager.IsAutoMode))
                    {
                        Debug.Log($"🟠 해당 역할 플레이어 없음 → AutoMode 트리거");
                        EnableAutoMode();
                    }
                    else
                    {
                        Debug.Log($"🟠 AutoMode 이미 활성화됨: 중복 트리거 방지");
                    }
                    return; // AutoMode일 때는 아래 로직 실행하지 않음
                }
            }

            // 네트워크 권한 체크를 우선적으로 사용
            if (networkManager != null)
            {
                bool hasPermission = networkManager.CanUserControlCurrentScript();
                Debug.Log($"🔵 UpdateUI 권한 체크 결과: {hasPermission}");
                Debug.Log($"  - 현재 사용자 역할: {PlayData.Instance?.GetLawCourtRole()}");
                Debug.Log($"  - 현재 스크립트 역할: {currentScript.Role}");
                Debug.Log($"  - 사용자 ID: {PlayData.User?.Id}");
                Debug.Log($"  - 스크립트 인덱스: {currentIndex}");
                SetNextStepButtonActive(hasPermission);
            }
            else
            {
                // 네트워크 매니저가 없는 경우 기존 로직 사용
                bool isMyRoleCache = IsMyRole();
                if (IsPlayerDataLoaded() && isMyRoleCache)
                {
                    SetNextStepButtonActive(true);
                }
                else
                {
                    SetNextStepButtonActive(false);
                }
            }
        }

        /// <summary>
        /// 오토모드의 경우 오토모드 뱃지 활성화를 하고 5초간 스크립트 보여주고 다음 스크립트로 이동동
        /// </summary>
        public void EnableAutoMode()
        {
            string dialoguePreview = currentScript?.Dialogue?.Length > 20 ? currentScript.Dialogue.Substring(0, 20) + "..." : currentScript?.Dialogue ?? "";
            Debug.Log($"🟡 EnableAutoMode 호출: 현재인덱스={currentIndex}, 역할={currentScript?.Role}, 로컬AutoMode={isAutoMode}, 네트워크AutoMode={networkManager?.IsAutoMode}");

            // 이미 AutoMode인 경우 중복 실행 방지
            if (isAutoMode)
            {
                Debug.Log($"🟡 로컬 AutoMode 이미 활성화됨: 중복 실행 방지");
                return;
            }

            // 네트워크 동기화
            if (networkManager != null)
            {
                // 네트워크에서 AutoMode가 이미 활성화되어 있는지 확인
                if (!networkManager.IsAutoMode)
                {
                    Debug.Log($"🟡 RPC_SetAutoMode(true) 호출");
                    networkManager.RPC_SetAutoMode(true, PlayData.User.Id);
                }
                else
                {
                    Debug.Log($"🟡 네트워크 AutoMode 이미 활성화됨: RPC 호출 생략");
                }
            }
            else
            {
                // 로컬 처리
                Debug.Log($"🟡 로컬 AutoMode 시작");
                isAutoMode = true;  // AutoMode 상태 설정
                EnableBadge(LawCourtRoleType.AUTO_MODE);
                SetNextStepButtonActive(false);
                StartCoroutine(AutoMode());
            }
        }

        IEnumerator AutoMode()
        {
            Debug.Log($"🟡 AutoMode 코루틴 시작: StateAuthority={networkManager?.Object?.HasStateAuthority}, 현재인덱스={currentIndex}");
            float remainingTime = 5f;

            while (remainingTime > 0)
            {
                Debug.Log($"⏰ AutoMode 남은 시간: {remainingTime:F0}초 (isAutoMode={isAutoMode})");

                yield return new WaitForSeconds(1f);
                remainingTime -= 1f;

                // AutoMode가 중간에 해제되었는지 확인
                if (!isAutoMode)
                {
                    Debug.Log($"🔴 AutoMode가 중간에 해제됨: 코루틴 종료");
                    yield break;
                }
            }

            Debug.Log($"🟡 AutoMode 타이머 완료: 카운팅 요청 시작 (현재인덱스={currentIndex})");

            // 네트워크 동기화로 AutoMode 종료
            if (networkManager != null)
            {
                Debug.Log($"🟡 RPC_SetAutoMode(false) 호출");
                networkManager.RPC_SetAutoMode(false, PlayData.User.Id);
                Debug.Log($"🟡 RPC_RequestNextScriptWithMode(true) 호출");
                // AutoMode에서는 권한 체크 없이 바로 다음 스크립트로 이동
                networkManager.RPC_RequestNextScriptWithMode(PlayData.User.Id, true);
            }
            else
            {
                // 로컬 처리
                isAutoMode = false;  // AutoMode 종료
                OnNextStepClicked();
            }
        }

        bool IsPlayerDataLoaded()
        {
            return PlayData.Instance != null;
        }

        bool IsMyRole()
        {
            return PlayData.Instance.GetLawCourtRole() == currentScript.Role;
        }

        /// <summary>
        /// 역할 표시 이름 반환
        /// </summary>
        private string GetRoleDisplayName(LawCourtRoleType role)
        {
            switch (role)
            {
                case LawCourtRoleType.JUDGE: return "판사";
                case LawCourtRoleType.PROSECUTOR: return "검사";
                case LawCourtRoleType.DEFENSE: return "변호사";
                case LawCourtRoleType.WITNESS: return "증인";
                case LawCourtRoleType.DEFENDANT: return "피고인";
                case LawCourtRoleType.AI_NARRATION: return "AI 설명";
                case LawCourtRoleType.JURY: return "배심원";
                case LawCourtRoleType.AUDIENCE: return "방청객";
                default: return "알 수 없음";
            }
        }

        /// <summary>
        /// nextStep 버튼 활성화/비활성화
        /// </summary>
        /// <param name="active">활성화 여부</param>
        public void SetNextStepButtonActive(bool active)
        {
            Debug.Log("SetNextStepButtonActive: " + active);
            if (nextStep != null)
            {
                nextStep.interactable = active;
                SetNextStepButtonColor(active);
            }
        }

        void SetNextStepButtonColor(bool active)
        {
            var btnCaolor = active ? new Color(0.2784314f, 0.4666667f, 0.9764706f, 1) : new Color(0.09411766f, 0.1647059f, 0.3333333f, 1);
            var textColor = active ? new Color(0.9450981f, 0.9568628f, 0.9686275f, 1) : new Color(0.5058824f, 0.5058824f, 0.5058824f, 1);

            nextStep.image.color = btnCaolor;
            nextStepBtnText.color = textColor;
        }

        /// <summary>
        /// nextStep 버튼 활성화
        /// </summary>
        public void EnableNextStepButton()
        {
            SetNextStepButtonActive(true);
        }

        /// <summary>
        /// nextStep 버튼 비활성화
        /// </summary>
        public void DisableNextStepButton()
        {
            SetNextStepButtonActive(false);
        }

        /// <summary>
        /// 큰 화면 보기 버튼 클릭
        /// </summary>
        public void OnLargeViewClicked()
        {
            StartCoroutine(AnimateToLargeView());
        }

        /// <summary>
        /// 작은 화면 보기 버튼 클릭
        /// </summary>
        public void OnSmallViewClicked()
        {
            StartCoroutine(AnimateToSmallView());
        }

        /// <summary>
        /// 큰 화면으로 애니메이션
        /// </summary>
        private IEnumerator AnimateToLargeView()
        {
            // 버튼 표시 상태 변경
            if (btnLargeView != null) btnLargeView.gameObject.SetActive(false);
            if (btnSmallView != null) btnSmallView.gameObject.SetActive(true);

            // 스크롤 설정 변경
            if (scrollRect != null)
            {
                // 스크롤 위치 초기화 (맨 위로)
                scrollRect.verticalNormalizedPosition = 1f;

                scrollRect.vertical = false;
                scrollRect.verticalScrollbar.transform.GetChild(0).gameObject.SetActive(false);
            }

            // 3개 애니메이션을 동시에 시작
            Coroutine anim1 = StartCoroutine(AnimateHeight(bgRT, 322f, 0.3f));
            Coroutine anim2 = StartCoroutine(AnimateHeight(contentsRT, 274f, 0.3f));
            Coroutine anim3 = StartCoroutine(AnimateHeight(bottomAreaRT, 230f, 0.3f));

            // 모든 애니메이션이 완료될 때까지 대기
            yield return anim1;
            yield return anim2;
            yield return anim3;
        }

        /// <summary>
        /// 작은 화면으로 애니메이션
        /// </summary>
        private IEnumerator AnimateToSmallView()
        {
            // 버튼 표시 상태 변경
            if (btnLargeView != null) btnLargeView.gameObject.SetActive(true);
            if (btnSmallView != null) btnSmallView.gameObject.SetActive(false);

            // 스크롤 설정 변경
            if (scrollRect != null)
            {
                scrollRect.vertical = true;
                scrollRect.verticalScrollbar.transform.GetChild(0).gameObject.SetActive(true);
            }

            // 3개 애니메이션을 동시에 시작
            Coroutine anim1 = StartCoroutine(AnimateHeight(bgRT, 222f, 0.3f));
            Coroutine anim2 = StartCoroutine(AnimateHeight(contentsRT, 174f, 0.3f));
            Coroutine anim3 = StartCoroutine(AnimateHeight(bottomAreaRT, 130f, 0.3f));

            // 모든 애니메이션이 완료될 때까지 대기
            yield return anim1;
            yield return anim2;
            yield return anim3;
        }

        /// <summary>
        /// RectTransform의 높이를 애니메이션으로 변경
        /// </summary>
        /// <param name="rectTransform">변경할 RectTransform</param>
        /// <param name="targetHeight">목표 높이</param>
        /// <param name="duration">애니메이션 시간</param>
        private IEnumerator AnimateHeight(RectTransform rectTransform, float targetHeight, float duration)
        {
            if (rectTransform == null) yield break;

            Vector2 startSize = rectTransform.sizeDelta;
            Vector2 targetSize = new Vector2(startSize.x, targetHeight);

            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // Smooth animation curve
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

                Vector2 currentSize = Vector2.Lerp(startSize, targetSize, smoothProgress);
                rectTransform.sizeDelta = currentSize;

                yield return null;
            }

            // 정확한 최종값 설정
            rectTransform.sizeDelta = targetSize;
        }
    }
}


