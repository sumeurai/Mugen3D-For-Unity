using System;
using System.Collections.Generic;

namespace SumeruAI.Samples
{
    public enum ImageMattingTaskStage
    {
        None,
        MattingUploading,
        MattingProcessing,
        MattingReady,
        SqSubmitting,
        SqProcessing,
        SqReady,
        HqSubmitting,
        HqProcessing,
        Completed,
        Failed
    }

    [Serializable]
    public class TaskData
    {
        public string localId;
        public string displayName;
        public string createdAtUtc;

        public string sourceImagePath;
        public string mattingTaskId;
        public string mattingStatus;
        public string mattingImageUrl;
        public string mattingPreviewLocalPath;

        public long mugenTaskId;
        public bool sqSubmitted;
        public bool hqSubmitted;
        public string generationStatus;
        public string generationFailureReason;
        public string downloadSqModelUrl;
        public string downloadHqModelUrl;
        public string downloadSqModelLocalPath;
        public string downloadHqModelLocalPath;
        [NonSerialized] public bool isDownloadingSq;
        [NonSerialized] public bool isDownloadingHq;
        [NonSerialized] public float downloadSqProgress;
        [NonSerialized] public float downloadHqProgress;

        public bool isMattingPolling;
        public bool isMugenPolling;
        public string lastError;
        public ImageMattingTaskStage stage;
    }

    [Serializable]
    public class ImageMattingTaskCollection
    {
        public List<TaskData> tasks = new List<TaskData>();
    }
}
