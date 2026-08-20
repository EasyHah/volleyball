using System;
using System.Threading;
using System.Threading.Tasks;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterTeacherReviewServiceV1
    {
        private readonly IMenShenChatClient _client;
        private readonly MenShenModelProfile _model;
        private readonly string _apiKey;
        private readonly TimeSpan _deadline;

        public SetterTeacherReviewServiceV1(
            IMenShenChatClient client,
            MenShenModelProfile model,
            string apiKey,
            TimeSpan deadline)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _model = model;
            _apiKey = string.IsNullOrWhiteSpace(apiKey)
                ? throw new ArgumentException("API key is required.", nameof(apiKey))
                : apiKey;
            _deadline = deadline > TimeSpan.Zero
                ? deadline
                : throw new ArgumentOutOfRangeException(nameof(deadline));
        }

        public async Task<SetterTeacherAttemptV1> RequestAsync(
            SetterTeacherRequestV1 request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            MenShenChatResult result;
            try
            {
                result = await _client.CompleteAsync(
                    _model,
                    SetterTeacherPromptV1.SystemPrompt,
                    SetterTeacherPromptV1.BuildCasePrompt(request),
                    _apiKey,
                    _deadline,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = new MenShenChatResult(MenShenChatStatus.Timeout, string.Empty, 0, 0, 0, 0, 0, 0, 0, string.Empty, "deadline");
            }
            catch (Exception exception)
            {
                return new SetterTeacherAttemptV1(request, _model,
                    new MenShenChatResult(MenShenChatStatus.GatewayError, string.Empty, 0, 0, 0, 0, 0, 0, 0, string.Empty, "client"),
                    null, exception.Message);
            }

            if (result.Status != MenShenChatStatus.Success)
                return new SetterTeacherAttemptV1(request, _model, result, null,
                    string.IsNullOrWhiteSpace(result.ErrorCategory) ? result.Status.ToString() : result.ErrorCategory);
            try
            {
                return new SetterTeacherAttemptV1(request, _model, result,
                    SetterTeacherResponseParserV1.Parse(result.Content, request), string.Empty);
            }
            catch (ArgumentException exception)
            {
                return new SetterTeacherAttemptV1(request, _model, result, null, exception.Message);
            }
        }
    }
}
