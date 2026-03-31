using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Gsplat;
using SumeruAI.API;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SumeruAI.Samples
{
    public class TaskListUI : MonoBehaviour
    {
        [Header("Top Actions")] 
        [SerializeField] private GameObject loginRoot;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button selectImageButton;
        [SerializeField] private Button refreshAllButton;

        [Header("Task List")]
        [SerializeField] private GameObject taskListUiRoot;
        [SerializeField] private Transform taskItemRoot;
        [SerializeField] private TaskItemUI taskItemPrefab;

        [Header("Runtime")]
        [SerializeField] private float pollingInterval = 10f;
        [SerializeField] private bool autoLoginOnStart;
        [SerializeField] private bool restorePollingOnStart = true;

        [Header("Model Viewer UI")]
        [SerializeField] private GameObject modelViewerOverlay;
        [SerializeField] private Text modelViewerStatusText;
        [SerializeField] private Button modelViewerBackButton;
        [SerializeField] private Button modelViewerRotateButton;
        [SerializeField] private Button modelViewerResetButton;
        [SerializeField] private Button modelViewerSqButton;
        [SerializeField] private Button modelViewerHqButton;

        [Header("Model Viewer Interaction")]
        [SerializeField] private float viewerDragRotateSpeed = 10f;
        [SerializeField] private float viewerDragPanSpeed = 0.1f;
        [SerializeField] private float viewerZoomStep = 0.15f;
        [SerializeField] private float viewerMinScale = 0.25f;
        [SerializeField] private float viewerMaxScale = 4f;

        private float MugenPollingInterval => APISettingsConfig.Instance.Mugen3DResultPollingIntervalSeconds;

        private readonly List<TaskData> tasks = new List<TaskData>();
        private readonly Dictionary<string, TaskItemUI> taskItems = new Dictionary<string, TaskItemUI>();
        private readonly Dictionary<string, Coroutine> mattingPollingCoroutines = new Dictionary<string, Coroutine>();
        private readonly Dictionary<string, Coroutine> mugenPollingCoroutines = new Dictionary<string, Coroutine>();
        private readonly Dictionary<string, Texture2D> previewTextures = new Dictionary<string, Texture2D>();

        private bool isModelViewerOpen;
        private bool isAutoRotatingModel = true;
        private bool isModelViewerUiBound;
        private Transform modelViewerRoot;
        private Transform modelViewerPivot;
        private GameObject currentSqModelObject;
        private GameObject currentHqModelObject;
        private GsplatAsset currentSqAsset;
        private GsplatAsset currentHqAsset;
        private float currentViewerScale = 1f;

        private void Awake()
        {
            BindTopButtons();
            TaskStore.EnsureDirectories();
        }

        private void Start()
        {
            LoadAndRestoreTasks();

            if (autoLoginOnStart)
            {
                Login();
            }
        }

        private void Update()
        {
            if (!isModelViewerOpen || modelViewerPivot == null)
            {
                return;
            }

            HandleModelViewerInput();

            if (isAutoRotatingModel && !IsManualViewerInteractionActive())
            {
                modelViewerPivot.Rotate(Vector3.up, 20f * Time.unscaledDeltaTime, Space.World);
            }
        }

        private void OnDestroy()
        {
            StopAllManagedCoroutines();
            DestroyAllPreviewTextures();
            DestroyLoadedViewerModels();
        }

        public void Login()
        {
            APIManager.Instance.Login(() =>
            {
                loginRoot.SetActive(false);
            });
        }

        public void SelectImageAndCreateTask()
        {
#if UNITY_EDITOR
            string path = EditorUtility.OpenFilePanel("Select Image for Matting", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            CreateTaskFromImagePath(path);
#else
            Debug.LogWarning("[ImageMattingUI] Runtime file selection is not implemented yet.");
#endif
        }

        public void CreateTaskFromImagePath(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                Debug.LogWarning($"[ImageMattingUI] Image file not found: {imagePath}");
                return;
            }

            TaskData task = WorkflowService.CreateTask(imagePath);
            tasks.Insert(0, task);
            CreateTaskItem(task);
            SaveTasks();
            RefreshTask(task);
            SubmitMatting(task);
        }

        public void RefreshAllTasks()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                TaskData task = tasks[i];
                if (task.isMattingPolling || task.stage == ImageMattingTaskStage.MattingProcessing)
                {
                    StartMattingPolling(task, true);
                }
                else if (task.isMugenPolling || task.stage == ImageMattingTaskStage.SqProcessing || task.stage == ImageMattingTaskStage.HqProcessing)
                {
                    StartMugenPolling(task, true);
                }
            }
        }

        private void BindTopButtons()
        {
            BindButton(loginButton, Login);
            BindButton(selectImageButton, SelectImageAndCreateTask);
            BindButton(refreshAllButton, RefreshAllTasks);
        }

        private void LoadAndRestoreTasks()
        {
            tasks.Clear();
            tasks.AddRange(TaskStore.LoadTasks());

            for (int i = 0; i < tasks.Count; i++)
            {
                TaskData task = tasks[i];
                CreateTaskItem(task);
                RestorePreview(task);

                if (!restorePollingOnStart)
                {
                    continue;
                }

                if (task.isMattingPolling || task.stage == ImageMattingTaskStage.MattingProcessing)
                {
                    StartMattingPolling(task);
                }
                else if (task.isMugenPolling || task.stage == ImageMattingTaskStage.SqProcessing || task.stage == ImageMattingTaskStage.HqProcessing)
                {
                    StartMugenPolling(task);
                }
            }
        }

        private void CreateTaskItem(TaskData task)
        {
            if (taskItemPrefab == null || taskItemRoot == null || task == null)
            {
                return;
            }

            if (taskItems.ContainsKey(task.localId))
            {
                return;
            }

            TaskItemUI item = Instantiate(taskItemPrefab, taskItemRoot);
            item.Bind(task, HandleGenerateSq, HandleGenerateHq, HandleDownloadSq, HandleDownloadHq, HandleRefreshSingleTask, HandleViewModel, HandleDeleteTask);
            taskItems.Add(task.localId, item);
        }

        private void RefreshTask(TaskData task)
        {
            if (task == null)
            {
                return;
            }

            WorkflowService.SyncLocalDownloadPaths(task);

            if (taskItems.TryGetValue(task.localId, out TaskItemUI item))
            {
                item.Refresh(task);
            }
        }

        private void HandleRefreshSingleTask(TaskData task)
        {
            if (task == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(task.mattingTaskId) && (task.isMattingPolling || task.stage == ImageMattingTaskStage.MattingProcessing))
            {
                StartMattingPolling(task, true);
                return;
            }

            if (task.mugenTaskId > 0)
            {
                StartMugenPolling(task, true);
            }
        }

        private void SubmitMatting(TaskData task)
        {
            try
            {
                task.stage = ImageMattingTaskStage.MattingUploading;
                task.lastError = string.Empty;
                task.isMattingPolling = false;
                SaveTasks();
                RefreshTask(task);

                WorkflowService.UploadImageMatting(
                    task.sourceImagePath,
                    (rep) =>
                    {
                        task.mattingTaskId = rep.data.ToString();
                        task.mattingStatus = "1";
                        task.stage = ImageMattingTaskStage.MattingProcessing;
                        task.isMattingPolling = true;
                        task.lastError = string.Empty;
                        SaveTasks();
                        RefreshTask(task);
                        StartMattingPolling(task, true);
                    },
                    (error) =>
                    {
                        task.stage = ImageMattingTaskStage.Failed;
                        task.lastError = error;
                        SaveTasks();
                        RefreshTask(task);
                        Debug.LogError($"[ImageMattingUI] Upload failed: {error}");
                    });
            }
            catch (Exception ex)
            {
                task.stage = ImageMattingTaskStage.Failed;
                task.lastError = ex.Message;
                SaveTasks();
                RefreshTask(task);
                Debug.LogError($"[ImageMattingUI] Upload exception: {ex.Message}");
            }
        }

        private void StartMattingPolling(TaskData task, bool queryImmediately = false)
        {
            if (task == null || string.IsNullOrEmpty(task.mattingTaskId))
            {
                return;
            }

            StopCoroutineIfExists(mattingPollingCoroutines, task.localId);
            task.isMattingPolling = true;
            SaveTasks();
            RefreshTask(task);
            mattingPollingCoroutines[task.localId] = StartCoroutine(MattingPollingRoutine(task.localId, queryImmediately));
        }

        private IEnumerator MattingPollingRoutine(string localId, bool queryImmediately)
        {
            if (!queryImmediately)
            {
                yield return new WaitForSecondsRealtime(MugenPollingInterval);
            }

            while (true)
            {
                TaskData task = FindTask(localId);
                if (task == null || string.IsNullOrEmpty(task.mattingTaskId))
                {
                    break;
                }

                bool completed = false;
                WorkflowService.QueryMattingResult(
                    task.mattingTaskId,
                    (rep) =>
                    {
                        HandleMattingResult(task, rep);
                        completed = true;
                    },
                    (error) =>
                    {
                        task.lastError = error;
                        SaveTasks();
                        RefreshTask(task);
                        completed = true;
                    });

                yield return new WaitUntil(() => completed);

                if (!task.isMattingPolling || task.stage != ImageMattingTaskStage.MattingProcessing)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(pollingInterval);
            }

            mattingPollingCoroutines.Remove(localId);
        }

        private void HandleMattingResult(TaskData task, ImageMattingResultRepData rep)
        {
            if (task == null || rep == null || rep.data == null)
            {
                return;
            }

            task.mattingStatus = rep.data.status;
            task.mattingImageUrl = rep.data.mattingImg;

            switch (rep.data.status)
            {
                case "1":
                    task.stage = ImageMattingTaskStage.MattingProcessing;
                    task.isMattingPolling = true;
                    break;
                case "2":
                    task.stage = ImageMattingTaskStage.MattingReady;
                    task.isMattingPolling = false;
                    StartCoroutine(CachePreview(task));
                    break;
                case "3":
                    task.stage = ImageMattingTaskStage.Failed;
                    task.isMattingPolling = false;
                    task.lastError = "Matting failed on server side.";
                    break;
                default:
                    task.stage = ImageMattingTaskStage.MattingProcessing;
                    task.isMattingPolling = true;
                    break;
            }

            SaveTasks();
            RefreshTask(task);
        }

        private IEnumerator CachePreview(TaskData task)
        {
            yield return WorkflowService.CachePreviewImage(
                task,
                (texture) =>
                {
                    SetPreview(task.localId, texture);
                    SaveTasks();
                    RefreshTask(task);
                },
                (error) =>
                {
                    task.lastError = $"Preview download failed: {error}";
                    SaveTasks();
                    RefreshTask(task);
                });
        }

        private void HandleGenerateSq(TaskData task)
        {
            if (!WorkflowService.CanGenerateSq(task))
            {
                return;
            }

            task.stage = ImageMattingTaskStage.SqSubmitting;
            task.lastError = string.Empty;
            SaveTasks();
            RefreshTask(task);

            StartCoroutine(SubmitSqRoutine(task.localId));
        }

        private IEnumerator SubmitSqRoutine(string localId)
        {
            TaskData task = FindTask(localId);
            if (task == null)
            {
                yield break;
            }

            yield return WorkflowService.SubmitSq(
                task,
                (rep) =>
                {
                    task.mugenTaskId = rep.data;
                    task.sqSubmitted = true;
                    task.stage = ImageMattingTaskStage.SqProcessing;
                    task.isMugenPolling = true;
                    task.generationFailureReason = string.Empty;
                    task.downloadSqModelUrl = string.Empty;
                    task.downloadHqModelUrl = string.Empty;
                    SaveTasks();
                    RefreshTask(task);
                    StartMugenPolling(task, true);
                },
                (error) =>
                {
                    task.stage = ImageMattingTaskStage.Failed;
                    task.lastError = error;
                    SaveTasks();
                    RefreshTask(task);
                });
        }

        private void HandleGenerateHq(TaskData task)
        {
            if (task == null || task.mugenTaskId <= 0)
            {
                return;
            }

            task.stage = ImageMattingTaskStage.HqSubmitting;
            task.lastError = string.Empty;
            SaveTasks();
            RefreshTask(task);

            WorkflowService.SubmitHq(
                task.mugenTaskId,
                (_) =>
                {
                    task.hqSubmitted = true;
                    task.stage = ImageMattingTaskStage.HqProcessing;
                    task.isMugenPolling = true;
                    SaveTasks();
                    RefreshTask(task);
                    StartMugenPolling(task, true);
                },
                (error) =>
                {
                    task.stage = ImageMattingTaskStage.Failed;
                    task.lastError = error;
                    SaveTasks();
                    RefreshTask(task);
                });
        }

        private void StartMugenPolling(TaskData task, bool queryImmediately = false)
        {
            if (task == null || task.mugenTaskId <= 0)
            {
                return;
            }

            StopCoroutineIfExists(mugenPollingCoroutines, task.localId);
            task.isMugenPolling = true;
            SaveTasks();
            RefreshTask(task);
            mugenPollingCoroutines[task.localId] = StartCoroutine(MugenPollingRoutine(task.localId, queryImmediately));
        }

        private IEnumerator MugenPollingRoutine(string localId, bool queryImmediately)
        {
            if (!queryImmediately)
            {
                yield return new WaitForSecondsRealtime(pollingInterval);
            }

            while (true)
            {
                TaskData task = FindTask(localId);
                if (task == null || task.mugenTaskId <= 0)
                {
                    break;
                }

                bool completed = false;
                WorkflowService.QueryMugenResult(
                    task.mugenTaskId,
                    (rep) =>
                    {
                        HandleMugenResult(task, rep);
                        completed = true;
                    },
                    (error) =>
                    {
                        task.lastError = error;
                        SaveTasks();
                        RefreshTask(task);
                        completed = true;
                    });

                yield return new WaitUntil(() => completed);

                if (!task.isMugenPolling)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(MugenPollingInterval);
            }

            mugenPollingCoroutines.Remove(localId);
        }

        private void HandleMugenResult(TaskData task, Mugen3DResultRepData rep)
        {
            if (task == null || rep == null || rep.data == null)
            {
                return;
            }

            task.generationStatus = rep.data.generationStatus;
            task.generationFailureReason = rep.data.generationFailureReason;
            task.downloadSqModelUrl = rep.data.downloadSqModelLink;
            task.downloadHqModelUrl = rep.data.downloadHqModelLink;

            if (!string.IsNullOrEmpty(task.generationFailureReason))
            {
                task.stage = ImageMattingTaskStage.Failed;
                task.isMugenPolling = false;
                task.lastError = task.generationFailureReason;
            }
            else if (task.hqSubmitted)
            {
                if (!string.IsNullOrEmpty(task.downloadHqModelUrl))
                {
                    task.stage = ImageMattingTaskStage.Completed;
                    task.isMugenPolling = false;
                }
                else
                {
                    task.stage = ImageMattingTaskStage.HqProcessing;
                    task.isMugenPolling = true;
                }
            }
            else if (task.sqSubmitted)
            {
                if (!string.IsNullOrEmpty(task.downloadSqModelUrl) || WorkflowService.IsMugenFinished(rep.data))
                {
                    task.stage = ImageMattingTaskStage.SqReady;
                    task.isMugenPolling = false;
                }
                else
                {
                    task.stage = ImageMattingTaskStage.SqProcessing;
                    task.isMugenPolling = !WorkflowService.IsMugenFinished(rep.data);
                }
            }

            SaveTasks();
            RefreshTask(task);
        }

        private void HandleDownloadSq(TaskData task)
        {
            if (!WorkflowService.CanDownloadSq(task))
            {
                RefreshTask(task);
                return;
            }

            DownloadModel(task, task.downloadSqModelUrl, false);
        }

        private void HandleDownloadHq(TaskData task)
        {
            if (!WorkflowService.CanDownloadHq(task))
            {
                RefreshTask(task);
                return;
            }

            DownloadModel(task, task.downloadHqModelUrl, true);
        }

        private void HandleViewModel(TaskData task)
        {
            if (!WorkflowService.CanViewModel(task))
            {
                RefreshTask(task);
                return;
            }

            StartCoroutine(OpenModelViewerRoutine(task));
        }

        private void HandleDeleteTask(TaskData task)
        {
            if (task == null)
            {
                return;
            }

            RemoveTask(task.localId);
        }

        private void DownloadModel(TaskData task, string url, bool isHq)
        {
            if (task == null)
            {
                return;
            }

            WorkflowService.SyncLocalDownloadPaths(task);

            string existingLocalPath = isHq ? task.downloadHqModelLocalPath : task.downloadSqModelLocalPath;
            if (WorkflowService.HasDownloadedFile(existingLocalPath))
            {
                RefreshTask(task);
                return;
            }

            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            string savePath = WorkflowService.GetAutoDownloadPath(task, url, isHq);
            if (WorkflowService.HasDownloadedFile(savePath))
            {
                SetDownloadedLocalPath(task, isHq, savePath);
                SaveTasks();
                RefreshTask(task);
                return;
            }

            SetDownloadState(task, isHq, true, 0f);
            task.lastError = string.Empty;
            SaveTasks();
            RefreshTask(task);

            WorkflowService.DownloadFile(
                url,
                savePath,
                (path) =>
                {
                    SetDownloadState(task, isHq, false, 1f);
                    SetDownloadedLocalPath(task, isHq, path);
                    SaveTasks();
                    RefreshTask(task);
                },
                (error) =>
                {
                    SetDownloadState(task, isHq, false, 0f);
                    task.lastError = error;
                    SaveTasks();
                    RefreshTask(task);
                    Debug.LogError($"[ImageMattingUI] Download failed: {error}");
                },
                (progress) =>
                {
                    SetDownloadState(task, isHq, true, progress);
                    RefreshTask(task);
                });
        }

        private void RestorePreview(TaskData task)
        {
            if (task == null || string.IsNullOrEmpty(task.mattingPreviewLocalPath))
            {
                return;
            }

            Texture2D texture = WorkflowService.LoadPreviewTexture(task.mattingPreviewLocalPath);
            SetPreview(task.localId, texture);
        }

        private void SetPreview(string localId, Texture2D texture)
        {
            if (FindTask(localId) == null)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }

                previewTextures.Remove(localId);
                return;
            }

            if (previewTextures.TryGetValue(localId, out Texture2D existing) && existing != null && existing != texture)
            {
                Destroy(existing);
            }

            if (texture != null)
            {
                previewTextures[localId] = texture;
            }
            else
            {
                previewTextures.Remove(localId);
            }

            if (taskItems.TryGetValue(localId, out TaskItemUI item))
            {
                item.SetPreviewTexture(texture);
            }
        }

        private TaskData FindTask(string localId)
        {
            return tasks.Find(task => task.localId == localId);
        }

        private void RemoveTask(string localId)
        {
            if (string.IsNullOrEmpty(localId))
            {
                return;
            }

            TaskData task = FindTask(localId);
            if (task == null)
            {
                return;
            }

            StopCoroutineIfExists(mattingPollingCoroutines, localId);
            StopCoroutineIfExists(mugenPollingCoroutines, localId);

            if (previewTextures.TryGetValue(localId, out Texture2D previewTexture))
            {
                if (previewTexture != null)
                {
                    Destroy(previewTexture);
                }

                previewTextures.Remove(localId);
            }

            if (taskItems.TryGetValue(localId, out TaskItemUI item))
            {
                taskItems.Remove(localId);
                Destroy(item.gameObject);
            }

            DeleteTaskLocalFiles(task);
            tasks.Remove(task);
            SaveTasks();
        }

        private void SaveTasks()
        {
            TaskStore.SaveTasks(tasks);
        }

        private static void DeleteTaskLocalFiles(TaskData task)
        {
            if (task == null)
            {
                return;
            }

            DeleteFileIfExists(task.mattingPreviewLocalPath);
            DeleteFileIfExists(task.downloadSqModelLocalPath);
            DeleteFileIfExists(task.downloadHqModelLocalPath);
        }

        private static void SetDownloadState(TaskData task, bool isHq, bool isDownloading, float progress)
        {
            if (task == null)
            {
                return;
            }

            float normalizedProgress = Mathf.Clamp01(progress);
            if (isHq)
            {
                task.isDownloadingHq = isDownloading;
                task.downloadHqProgress = normalizedProgress;
            }
            else
            {
                task.isDownloadingSq = isDownloading;
                task.downloadSqProgress = normalizedProgress;
            }
        }

        private static void SetDownloadedLocalPath(TaskData task, bool isHq, string localPath)
        {
            if (task == null)
            {
                return;
            }

            if (isHq)
            {
                task.downloadHqModelLocalPath = localPath;
            }
            else
            {
                task.downloadSqModelLocalPath = localPath;
            }
        }

        private static void DeleteFileIfExists(string localPath)
        {
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                return;
            }

            try
            {
                File.Delete(localPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ImageMattingUI] Failed to delete local file: {localPath}. {ex.Message}");
            }
        }

        private IEnumerator OpenModelViewerRoutine(TaskData task)
        {
            if (!EnsureModelViewerUi())
            {
                yield break;
            }

            SetListUiVisible(false);
            SetModelViewerActive(true);
            SetModelViewerStatus("Loading model...");
            SetModelViewerButtonsInteractable(false);

            isAutoRotatingModel = true;
            RefreshModelViewerRotateLabel();

            yield return null;

            DestroyLoadedViewerModels();

            bool hasSq = TryCreateViewerModel(task.downloadSqModelLocalPath, false, out currentSqModelObject, out currentSqAsset);
            bool hasHq = TryCreateViewerModel(task.downloadHqModelLocalPath, true, out currentHqModelObject, out currentHqAsset);

            if (!hasSq && !hasHq)
            {
                SetModelViewerStatus("No local SQ/HQ model available.");
                CloseModelViewer();
                yield break;
            }

            ResetModelViewerTransform();
            bool showHq = hasHq;
            SetActiveViewerModel(showHq);
            SetModelViewerButtonsInteractable(true);
            UpdateModelViewerVariantButtons();
            SetModelViewerStatus(showHq ? "HQ model loaded." : "SQ model loaded.");
        }

        private bool TryCreateViewerModel(string localPath, bool isHq, out GameObject modelObject, out GsplatAsset gsplatAsset)
        {
            modelObject = null;
            gsplatAsset = null;

            if (!WorkflowService.HasDownloadedFile(localPath))
            {
                return false;
            }

            if (!GsplatLoader.TryLoadAsset(localPath, out gsplatAsset, out string error))
            {
                Debug.LogError($"[ImageMattingUI] {(isHq ? "HQ" : "SQ")} model load failed: {error}");
                SetModelViewerStatus(error);
                return false;
            }

            modelObject = CreateViewerModelObject(isHq ? "HQ Model" : "SQ Model", gsplatAsset);
            return modelObject != null;
        }

        private GameObject CreateViewerModelObject(string name, GsplatAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            EnsureViewerModelRoot();

            GameObject modelObject = new GameObject(name);
            modelObject.transform.SetParent(modelViewerPivot, false);
            modelObject.transform.localPosition = Vector3.zero;
            modelObject.transform.localRotation = Quaternion.identity;
            modelObject.transform.localScale = new Vector3(1f, 1f, -1f);

            GsplatRenderer renderer = modelObject.AddComponent<GsplatRenderer>();
            renderer.GsplatAsset = asset;
            renderer.SHDegree = 3;
            renderer.GammaToLinear = true;
            return modelObject;
        }

        private void EnsureViewerModelRoot()
        {
            if (modelViewerRoot != null && modelViewerPivot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("ModelViewerRoot");
            modelViewerRoot = rootObject.transform;
            modelViewerRoot.SetParent(null, false);
            modelViewerRoot.position = Vector3.zero;

            GameObject pivotObject = new GameObject("ModelViewerPivot");
            modelViewerPivot = pivotObject.transform;
            modelViewerPivot.SetParent(modelViewerRoot, false);
        }

        private void SetActiveViewerModel(bool showHq)
        {
            bool hasHq = currentHqModelObject != null;
            bool hasSq = currentSqModelObject != null;

            if (currentSqModelObject != null)
            {
                currentSqModelObject.SetActive(hasSq && !showHq);
            }

            if (currentHqModelObject != null)
            {
                currentHqModelObject.SetActive(hasHq && showHq);
            }

            UpdateModelViewerVariantButtons();
        }

        private bool EnsureModelViewerUi()
        {
            if (modelViewerOverlay == null)
            {
                Debug.LogWarning("[ImageMattingUI] Please assign Model Viewer Overlay in the inspector.");
                return false;
            }

            if (isModelViewerUiBound)
            {
                return true;
            }

            BindButton(modelViewerBackButton, CloseModelViewer);
            BindButton(modelViewerRotateButton, ToggleAutoRotate);
            BindButton(modelViewerResetButton, ResetModelViewerTransform);
            BindButton(modelViewerSqButton, () => SetActiveViewerModel(false));
            BindButton(modelViewerHqButton, () => SetActiveViewerModel(true));
            modelViewerOverlay.SetActive(false);
            isModelViewerUiBound = true;
            return true;
        }

        private void SetModelViewerActive(bool isActive)
        {
            isModelViewerOpen = isActive;
            if (modelViewerOverlay != null)
            {
                modelViewerOverlay.SetActive(isActive);
            }
        }

        private void SetListUiVisible(bool visible)
        {
            if (taskListUiRoot != null)
            {
                taskListUiRoot.SetActive(visible);
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.gameObject != modelViewerOverlay)
                {
                    child.gameObject.SetActive(visible);
                }
            }
        }

        private void SetModelViewerStatus(string status)
        {
            if (modelViewerStatusText != null)
            {
                modelViewerStatusText.text = status;
            }
        }

        private void SetModelViewerButtonsInteractable(bool interactable)
        {
            if (modelViewerBackButton != null)
            {
                modelViewerBackButton.interactable = interactable;
            }

            if (modelViewerRotateButton != null)
            {
                modelViewerRotateButton.interactable = interactable;
            }

            if (modelViewerResetButton != null)
            {
                modelViewerResetButton.interactable = interactable;
            }
        }

        private void UpdateModelViewerVariantButtons()
        {
            if (modelViewerSqButton != null)
            {
                modelViewerSqButton.interactable = currentSqModelObject != null;
            }

            if (modelViewerHqButton != null)
            {
                modelViewerHqButton.interactable = currentHqModelObject != null;
            }
        }

        private void ToggleAutoRotate()
        {
            isAutoRotatingModel = !isAutoRotatingModel;
            RefreshModelViewerRotateLabel();
        }

        private void RefreshModelViewerRotateLabel()
        {
            if (modelViewerRotateButton == null)
            {
                return;
            }

            Text label = modelViewerRotateButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = isAutoRotatingModel ? "Auto Rotate: On" : "Auto Rotate: Off";
            }
        }

        private void CloseModelViewer()
        {
            DestroyLoadedViewerModels();
            SetModelViewerActive(false);
            SetListUiVisible(true);
        }

        private void DestroyLoadedViewerModels()
        {
            if (currentSqModelObject != null)
            {
                Destroy(currentSqModelObject);
                currentSqModelObject = null;
            }

            if (currentHqModelObject != null)
            {
                Destroy(currentHqModelObject);
                currentHqModelObject = null;
            }

            if (modelViewerRoot != null)
            {
                Destroy(modelViewerRoot.gameObject);
                modelViewerRoot = null;
                modelViewerPivot = null;
            }

            if (currentSqAsset != null)
            {
                Destroy(currentSqAsset);
                currentSqAsset = null;
            }

            if (currentHqAsset != null)
            {
                Destroy(currentHqAsset);
                currentHqAsset = null;
            }
        }

        private void HandleModelViewerInput()
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.001f)
            {
                SetViewerScale(currentViewerScale + scrollDelta * viewerZoomStep);
            }

            bool isRotateDragging = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            bool isPanDragging = Input.GetMouseButton(2);
            if (!isRotateDragging && !isPanDragging)
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            float deltaX = Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");
            if (Mathf.Abs(deltaX) <= 0.0001f && Mathf.Abs(deltaY) <= 0.0001f)
            {
                return;
            }

            Camera activeCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            Vector3 upAxis = activeCamera != null ? activeCamera.transform.up : Vector3.up;
            Vector3 rightAxis = activeCamera != null ? activeCamera.transform.right : Vector3.right;

            if (isRotateDragging)
            {
                modelViewerPivot.Rotate(upAxis, -deltaX * viewerDragRotateSpeed * 100f * Time.unscaledDeltaTime, Space.World);
                modelViewerPivot.Rotate(rightAxis, deltaY * viewerDragRotateSpeed * 100f * Time.unscaledDeltaTime, Space.World);
            }

            if (isPanDragging && modelViewerRoot != null)
            {
                float panFactor = viewerDragPanSpeed * Mathf.Max(0.25f, currentViewerScale);
                Vector3 offset = (-rightAxis * deltaX + -upAxis * deltaY) * panFactor;
                modelViewerRoot.position += offset;
            }
        }

        private void ResetModelViewerTransform()
        {
            if (modelViewerPivot == null || modelViewerRoot == null)
            {
                return;
            }

            modelViewerRoot.position = Vector3.zero;
            modelViewerPivot.localPosition = Vector3.zero;
            modelViewerPivot.localRotation = Quaternion.identity;
            SetViewerScale(1f);
        }

        private void SetViewerScale(float scale)
        {
            currentViewerScale = Mathf.Clamp(scale, viewerMinScale, viewerMaxScale);
            if (modelViewerPivot != null)
            {
                modelViewerPivot.localScale = Vector3.one * currentViewerScale;
            }
        }

        private bool IsManualViewerInteractionActive()
        {
            return (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) && !IsPointerOverUi();
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void StopAllManagedCoroutines()
        {
            StopCoroutineGroup(mattingPollingCoroutines);
            StopCoroutineGroup(mugenPollingCoroutines);
        }

        private void StopCoroutineGroup(Dictionary<string, Coroutine> coroutineMap)
        {
            List<Coroutine> coroutines = new List<Coroutine>(coroutineMap.Values);
            for (int i = 0; i < coroutines.Count; i++)
            {
                if (coroutines[i] != null)
                {
                    StopCoroutine(coroutines[i]);
                }
            }

            coroutineMap.Clear();
        }

        private void StopCoroutineIfExists(Dictionary<string, Coroutine> coroutineMap, string key)
        {
            if (!coroutineMap.TryGetValue(key, out Coroutine coroutine))
            {
                return;
            }

            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }

            coroutineMap.Remove(key);
        }

        private void DestroyAllPreviewTextures()
        {
            foreach (Texture2D texture in previewTextures.Values)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }

            previewTextures.Clear();
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }
        }
    }
}
