namespace Common.Constants
{
    public static class EKYCConstant
    {
        public const string FileUpload = "/file-service/v1/addFile";
        public const string ClassifyId = "/ai/v1/classify/id";

        // QUAN TRỌNG: Dùng endpoint WEB
        public const string CardLiveness = "/ai/v1/web/card/liveness";
        public const string OcrFront = "/ai/v1/web/ocr/id/front";
        public const string OcrBack = "/ai/v1/web/ocr/id/back";
        public const string OcrFull = "/ai/v1/web/ocr/id"; // Hoặc /ai/v1/ocr/id tùy config server

        public const string FaceCompare = "/ai/v1/face/compare";
        public const string FaceLiveness = "/ai/v1/face/liveness";
        public const string FaceMask = "/ai/v1/face/mask";

        public const string FaceSearch = "/face-service/face/search";
        public const string FaceSearchK = "/face-service/face/search-k";
        public const string CompareGeneral = "/v1/face/compare-general";
    }
}