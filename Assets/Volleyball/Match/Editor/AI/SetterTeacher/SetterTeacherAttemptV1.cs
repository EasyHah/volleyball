using System;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterTeacherAttemptV1
    {
        internal SetterTeacherAttemptV1(
            SetterTeacherRequestV1 request,
            MenShenModelProfile model,
            MenShenChatResult result,
            SetterTeacherResponseV1 response,
            string error)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Model = model;
            Result = result;
            Response = response;
            Error = error ?? string.Empty;
            CreatedUtc = DateTime.UtcNow;
        }

        public SetterTeacherRequestV1 Request { get; }
        public MenShenModelProfile Model { get; }
        public MenShenChatResult Result { get; }
        public SetterTeacherResponseV1 Response { get; }
        public string Error { get; }
        public DateTime CreatedUtc { get; }
        public bool IsSuccessful => Result.Status == MenShenChatStatus.Success && Response != null;
    }
}
