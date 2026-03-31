using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SumeruAI.Samples
{
    public class TaskItemUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text stageText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text localPathText;
        [SerializeField] private RawImage previewImage;

        [Header("Actions")]
        [SerializeField] private Button generateSqButton;
        [SerializeField] private Button generateHqButton;
        [SerializeField] private Button downloadSqButton;
        [SerializeField] private Button downloadHqButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button viewModelButton;
        [SerializeField] private Button deleteButton;

        private TaskData task;
        private Action<TaskData> onGenerateSq;
        private Action<TaskData> onGenerateHq;
        private Action<TaskData> onDownloadSq;
        private Action<TaskData> onDownloadHq;
        private Action<TaskData> onRefresh;
        private Action<TaskData> onViewModel;
        private Action<TaskData> onDelete;
        private ConfirmationDialogUI confirmationDialog;

        public void Bind(
            TaskData taskData,
            Action<TaskData> handleGenerateSq,
            Action<TaskData> handleGenerateHq,
            Action<TaskData> handleDownloadSq,
            Action<TaskData> handleDownloadHq,
            Action<TaskData> handleRefresh,
            Action<TaskData> handleViewModel,
            Action<TaskData> handleDelete)
        {
            task = taskData;
            onGenerateSq = handleGenerateSq;
            onGenerateHq = handleGenerateHq;
            onDownloadSq = handleDownloadSq;
            onDownloadHq = handleDownloadHq;
            onRefresh = handleRefresh;
            onViewModel = handleViewModel;
            onDelete = handleDelete;

            EnsureViewModelButton();
            EnsureDeleteButton();
            EnsureConfirmationDialog();

            BindButton(generateSqButton, () => onGenerateSq?.Invoke(task));
            BindButton(generateHqButton, () => onGenerateHq?.Invoke(task));
            BindButton(downloadSqButton, () => onDownloadSq?.Invoke(task));
            BindButton(downloadHqButton, () => onDownloadHq?.Invoke(task));
            BindButton(refreshButton, () => onRefresh?.Invoke(task));
            BindButton(viewModelButton, () => onViewModel?.Invoke(task));
            BindButton(deleteButton, ShowDeleteConfirmation);

            Refresh(taskData);
        }

        public void Refresh(TaskData taskData)
        {
            task = taskData;
            WorkflowService.SyncLocalDownloadPaths(task);

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(task.displayName) ? "Image Matting Task" : task.displayName;
            }

            if (stageText != null)
            {
                stageText.text = $"Stage: {task.stage}";
            }

            if (statusText != null)
            {
                string sqStatus = task.sqSubmitted ? "Submitted" : "Not Submitted";
                string hqStatus = task.hqSubmitted ? "Submitted" : "Not Submitted";
                statusText.text = $"Matting: {task.mattingStatus} | SQ: {sqStatus} | HQ: {hqStatus}";
            }

            if (detailText != null)
            {
                string generation = string.IsNullOrEmpty(task.generationStatus) ? "-" : task.generationStatus;
                string failure = string.IsNullOrEmpty(task.generationFailureReason) ? "-" : task.generationFailureReason;
                detailText.text =
                    // $"Matting Task: {task.mattingTaskId}\n" +
                    // $"Mugen Task: {(task.mugenTaskId > 0 ? task.mugenTaskId.ToString() : "-")}\n" +
                    // $"Generation: {generation}\n" +
                    $"SQ Download: {GetDownloadStatus(task.downloadSqModelUrl, task.downloadSqModelLocalPath, task.isDownloadingSq, task.downloadSqProgress)}\n" +
                    $"HQ Download: {GetDownloadStatus(task.downloadHqModelUrl, task.downloadHqModelLocalPath, task.isDownloadingHq, task.downloadHqProgress)}\n" +
                    $"Failure: {failure}";
            }

            if (localPathText != null)
            {
                string sqLocal = string.IsNullOrEmpty(task.downloadSqModelLocalPath) ? "-" : task.downloadSqModelLocalPath;
                string hqLocal = string.IsNullOrEmpty(task.downloadHqModelLocalPath) ? "-" : task.downloadHqModelLocalPath;
                localPathText.text = $"SQ Local: {sqLocal}\nHQ Local: {hqLocal}";
            }

            if (generateSqButton != null)
            {
                generateSqButton.interactable = WorkflowService.CanGenerateSq(task);
            }

            if (generateHqButton != null)
            {
                generateHqButton.interactable = WorkflowService.CanGenerateHq(task);
            }

            if (downloadSqButton != null)
            {
                downloadSqButton.interactable = WorkflowService.CanDownloadSq(task);
            }

            if (downloadHqButton != null)
            {
                downloadHqButton.interactable = WorkflowService.CanDownloadHq(task);
            }

            if (viewModelButton != null)
            {
                viewModelButton.interactable = WorkflowService.CanViewModel(task);
            }

            if (deleteButton != null)
            {
                deleteButton.interactable = task != null;
            }
        }

        public void SetPreviewTexture(Texture texture)
        {
            if (previewImage != null)
            {
                previewImage.texture = texture;
                previewImage.color = texture != null ? Color.white : Color.clear;
                FitPreviewToParent(texture);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (previewImage == null || previewImage.texture == null)
            {
                return;
            }

            FitPreviewToParent(previewImage.texture);
        }

        private void OnDestroy()
        {
            if (confirmationDialog != null)
            {
                confirmationDialog.Dispose();
                confirmationDialog = null;
            }
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

        private static string GetDownloadStatus(string downloadUrl, string localPath, bool isDownloading, float progress)
        {
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                return "Downloaded";
            }

            if (isDownloading)
            {
                return $"Downloading {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
            }

            return string.IsNullOrEmpty(downloadUrl) ? "Unavailable" : "Ready";
        }

        private void FitPreviewToParent(Texture texture)
        {
            if (previewImage == null)
            {
                return;
            }

            RectTransform previewRect = previewImage.rectTransform;
            RectTransform parentRect = previewRect.parent as RectTransform;
            if (previewRect == null || parentRect == null)
            {
                return;
            }

            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                previewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
                previewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
                return;
            }

            float availableWidth = Mathf.Max(0f, parentRect.rect.width);
            float availableHeight = Mathf.Max(0f, parentRect.rect.height);
            if (availableWidth <= 0f || availableHeight <= 0f)
            {
                return;
            }

            float textureAspect = (float)texture.width / texture.height;
            float parentAspect = availableWidth / availableHeight;

            float targetWidth = availableWidth;
            float targetHeight = availableHeight;
            if (textureAspect > parentAspect)
            {
                targetHeight = targetWidth / textureAspect;
            }
            else
            {
                targetWidth = targetHeight * textureAspect;
            }

            previewRect.anchorMin = new Vector2(0f, 0.5f);
            previewRect.anchorMax = new Vector2(0f, 0.5f);
            previewRect.pivot = new Vector2(0f, 0.5f);
            previewRect.anchoredPosition = Vector2.zero;
            previewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
            previewRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        }

        private void EnsureViewModelButton()
        {
            if (viewModelButton != null)
            {
                return;
            }

            RectTransform rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            GameObject buttonObject = new GameObject("view-model-btn", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.layer = gameObject.layer;

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(rootRect, false);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(1140f, 40f);
            buttonRect.sizeDelta = new Vector2(180f, 65f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.5764706f, 0.5764706f, 0.5764706f, 1f);

            viewModelButton = buttonObject.GetComponent<Button>();
            ColorBlock colors = viewModelButton.colors;
            colors.normalColor = new Color(0.5764706f, 0.5764706f, 0.5764706f, 1f);
            colors.highlightedColor = new Color(0.48235294f, 0.48235294f, 0.48235294f, 1f);
            colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
            colors.selectedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
            colors.disabledColor = new Color(0.2735849f, 0f, 0f, 0.5019608f);
            viewModelButton.colors = colors;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelObject.layer = gameObject.layer;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.GetComponent<Text>();
            label.text = "View Model";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 26;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;
        }

        private void EnsureDeleteButton()
        {
            if (deleteButton != null)
            {
                return;
            }

            RectTransform rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            GameObject buttonObject = new GameObject("delete-btn", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.layer = gameObject.layer;

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(rootRect, false);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(1310f, 40f);
            buttonRect.sizeDelta = new Vector2(120f, 65f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.78039217f, 0.25490198f, 0.25490198f, 1f);

            deleteButton = buttonObject.GetComponent<Button>();
            ColorBlock colors = deleteButton.colors;
            colors.normalColor = new Color(0.78039217f, 0.25490198f, 0.25490198f, 1f);
            colors.highlightedColor = new Color(0.6862745f, 0.20392157f, 0.20392157f, 1f);
            colors.pressedColor = new Color(0.8901961f, 0.37254903f, 0.37254903f, 1f);
            colors.selectedColor = new Color(0.8901961f, 0.37254903f, 0.37254903f, 1f);
            colors.disabledColor = new Color(0.2735849f, 0f, 0f, 0.5019608f);
            deleteButton.colors = colors;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelObject.layer = gameObject.layer;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.GetComponent<Text>();
            label.text = "Delete";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 24;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;
        }

        private void EnsureConfirmationDialog()
        {
            if (confirmationDialog != null)
            {
                return;
            }

            confirmationDialog = ConfirmationDialogUI.Create(gameObject.layer);
        }

        private void ShowDeleteConfirmation()
        {
            EnsureConfirmationDialog();
            confirmationDialog?.Show(
                $"Delete \"{GetTaskDisplayName()}\"?\nThis will remove the task and local cached files.",
                onConfirm: () => onDelete?.Invoke(task),
                confirmButtonText: "Delete",
                cancelButtonText: "Cancel");
        }

        private string GetTaskDisplayName()
        {
            return task == null || string.IsNullOrEmpty(task.displayName) ? "this task" : task.displayName;
        }
    }
}
