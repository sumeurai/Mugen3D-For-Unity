using System;
using System.Collections;
using System.IO;
using SumeruAI.API;
using UnityEngine;
using UnityEngine.Networking;

namespace SumeruAI.Samples
{
    public static class WorkflowService
    {
        public static TaskData CreateTask(string sourceImagePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(sourceImagePath);
            return new TaskData
            {
                localId = Guid.NewGuid().ToString("N"),
                displayName = string.IsNullOrEmpty(fileName) ? "Image Matting Task" : fileName,
                createdAtUtc = DateTime.UtcNow.ToString("o"),
                sourceImagePath = sourceImagePath,
                mattingStatus = "0",
                stage = ImageMattingTaskStage.MattingUploading
            };
        }

        public static bool CanGenerateSq(TaskData task)
        {
            return task != null
                && !string.IsNullOrEmpty(task.mattingImageUrl)
                && task.mugenTaskId <= 0;
        }

        public static bool CanGenerateHq(TaskData task)
        {
            return task != null
                && task.mugenTaskId > 0
                && (!string.IsNullOrEmpty(task.downloadSqModelUrl) || !string.IsNullOrEmpty(task.downloadSqModelLocalPath))
                && string.IsNullOrEmpty(task.downloadHqModelUrl);
        }

        public static bool CanDownloadSq(TaskData task)
        {
            if (task == null)
            {
                return false;
            }

            SyncLocalDownloadPaths(task);
            return !IsAnyDownloadInProgress(task)
                && !HasDownloadedFile(task.downloadSqModelLocalPath)
                && !string.IsNullOrEmpty(task.downloadSqModelUrl);
        }

        public static bool CanDownloadHq(TaskData task)
        {
            if (task == null)
            {
                return false;
            }

            SyncLocalDownloadPaths(task);
            return !IsAnyDownloadInProgress(task)
                && !HasDownloadedFile(task.downloadHqModelLocalPath)
                && !string.IsNullOrEmpty(task.downloadHqModelUrl);
        }

        public static bool CanViewModel(TaskData task)
        {
            if (task == null)
            {
                return false;
            }

            SyncLocalDownloadPaths(task);
            return HasDownloadedFile(task.downloadSqModelLocalPath)
                || HasDownloadedFile(task.downloadHqModelLocalPath);
        }

        public static void UploadImageMatting(
            string imagePath,
            Action<ImageMattingRepData> onSuccess,
            Action<string> onError)
        {
            byte[] imageData = File.ReadAllBytes(imagePath);
            string fileName = Path.GetFileName(imagePath);
            APIManager.Instance.UploadImageMatting(imageData, fileName, onSuccess, onError);
        }

        public static void QueryMattingResult(
            string taskId,
            Action<ImageMattingResultRepData> onSuccess,
            Action<string> onError)
        {
            APIManager.Instance.GetImageMattingResult(taskId, onSuccess, onError);
        }

        public static IEnumerator SubmitSq(
            TaskData task,
            Action<Mugen3DSqRepData> onSuccess,
            Action<string> onError)
        {
            if (task == null)
            {
                onError?.Invoke("Task is null.");
                yield break;
            }

            bool bytesLoaded = false;
            byte[] imageBytes = null;
            string fileName = GetSqUploadFileName(task);

            if (!string.IsNullOrEmpty(task.mattingPreviewLocalPath) && File.Exists(task.mattingPreviewLocalPath))
            {
                imageBytes = File.ReadAllBytes(task.mattingPreviewLocalPath);
                bytesLoaded = true;
            }
            else
            {
                yield return DownloadImageBytes(
                    task.mattingImageUrl,
                    (bytes) =>
                    {
                        imageBytes = bytes;
                        bytesLoaded = true;
                    },
                    onError);
            }

            if (!bytesLoaded || imageBytes == null || imageBytes.Length == 0)
            {
                onError?.Invoke("SQ upload image data is empty.");
                yield break;
            }

            bool requestCompleted = false;
            APIManager.Instance.SubmitMugen3DSq(
                imageBytes,
                fileName,
                (rep) =>
                {
                    requestCompleted = true;
                    onSuccess?.Invoke(rep);
                },
                (error) =>
                {
                    requestCompleted = true;
                    onError?.Invoke(error);
                });

            yield return new WaitUntil(() => requestCompleted);
        }

        public static void SubmitHq(
            long taskId,
            Action<Mugen3DHqRepData> onSuccess,
            Action<string> onError)
        {
            APIManager.Instance.SubmitMugen3DHq(taskId, onSuccess, onError);
        }

        public static void QueryMugenResult(
            long taskId,
            Action<Mugen3DResultRepData> onSuccess,
            Action<string> onError)
        {
            APIManager.Instance.GetMugen3DResult(taskId, onSuccess, onError);
        }

        public static IEnumerator CachePreviewImage(
            TaskData task,
            Action<Texture2D> onSuccess,
            Action<string> onError)
        {
            if (task == null || string.IsNullOrEmpty(task.mattingImageUrl))
            {
                onError?.Invoke("Preview image URL is empty.");
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(task.mattingImageUrl))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error);
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    onError?.Invoke("Preview texture is null.");
                    yield break;
                }

                byte[] pngBytes = texture.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    onError?.Invoke("Failed to encode preview texture.");
                    yield break;
                }

                string savePath = TaskStore.GetPreviewPath(task.localId);
                File.WriteAllBytes(savePath, pngBytes);
                task.mattingPreviewLocalPath = savePath;
                onSuccess?.Invoke(texture);
            }
        }

        public static Texture2D LoadPreviewTexture(string localPath)
        {
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(localPath);
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }

        public static void DownloadFile(
            string url,
            string savePath,
            Action<string> onSuccess,
            Action<string> onError,
            Action<float> onProgress = null)
        {
            APIManager.Instance.DownloadFile(url, savePath, onSuccess, onError, onProgress);
        }

        public static void SyncLocalDownloadPaths(TaskData task)
        {
            if (task == null)
            {
                return;
            }

            if (!HasDownloadedFile(task.downloadSqModelLocalPath))
            {
                task.downloadSqModelLocalPath = string.Empty;
            }

            if (!HasDownloadedFile(task.downloadHqModelLocalPath))
            {
                task.downloadHqModelLocalPath = string.Empty;
            }
        }

        public static bool HasDownloadedFile(string localPath)
        {
            return !string.IsNullOrEmpty(localPath) && File.Exists(localPath);
        }

        public static string GetAutoDownloadPath(TaskData task, string url, bool isHq)
        {
            TaskStore.EnsureDirectories();

            string extension = GetExtensionFromUrl(url, ".zip");
            string taskId = task != null && task.mugenTaskId > 0 ? task.mugenTaskId.ToString() : "task";
            string suffix = isHq ? "hq" : "sq";
            return Path.Combine(TaskStore.DownloadDirectory, $"{taskId}_{suffix}{extension}");
        }

        public static bool IsMugenFinished(Mugen3DResultBodyData data)
        {
            if (data == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(data.downloadHqModelLink))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(data.downloadSqModelLink) && string.IsNullOrEmpty(data.generationStatus))
            {
                return true;
            }

            string status = data.generationStatus ?? string.Empty;
            return status.Equals("success", StringComparison.OrdinalIgnoreCase)
                || status.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
                || status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("done", StringComparison.OrdinalIgnoreCase)
                || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("fail", StringComparison.OrdinalIgnoreCase)
                || status.Equals("error", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetSuggestedFileName(string url, string fallbackName)
        {
            try
            {
                string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                return string.IsNullOrEmpty(fileName) ? fallbackName : fileName;
            }
            catch
            {
                return fallbackName;
            }
        }

        private static bool IsAnyDownloadInProgress(TaskData task)
        {
            return task != null && (task.isDownloadingSq || task.isDownloadingHq);
        }

        private static string GetExtensionFromUrl(string url, string fallbackExtension)
        {
            try
            {
                string extension = Path.GetExtension(new Uri(url).AbsolutePath);
                return string.IsNullOrEmpty(extension) ? fallbackExtension : extension;
            }
            catch
            {
                return fallbackExtension;
            }
        }

        private static IEnumerator DownloadImageBytes(
            string imageUrl,
            Action<byte[]> onSuccess,
            Action<string> onError)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                onError?.Invoke("Image URL is empty.");
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(imageUrl))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error);
                    yield break;
                }

                byte[] bytes = request.downloadHandler.data;
                if (bytes == null || bytes.Length == 0)
                {
                    onError?.Invoke("Downloaded image bytes are empty.");
                    yield break;
                }

                onSuccess?.Invoke(bytes);
            }
        }

        private static string GetSqUploadFileName(TaskData task)
        {
            if (task == null)
            {
                return "matting_result.png";
            }

            if (!string.IsNullOrEmpty(task.mattingPreviewLocalPath))
            {
                string previewName = Path.GetFileName(task.mattingPreviewLocalPath);
                if (!string.IsNullOrEmpty(previewName))
                {
                    return previewName;
                }
            }

            string baseName = string.IsNullOrEmpty(task.displayName) ? task.localId : task.displayName;
            return $"{baseName}_matting.png";
        }
    }
}
