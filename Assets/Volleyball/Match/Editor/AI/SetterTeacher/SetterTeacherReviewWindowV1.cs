using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Volleyball.Presentation.TrainingLab;

namespace Volleyball.Editor.AI.SetterTeacher
{
    public sealed class SetterTeacherReviewWindowV1 : EditorWindow
    {
        private int _snapshotIndex;
        private int _selectedCandidateIndex;
        private bool _acceptTeacher = true;
        private string _correctionReason = string.Empty;
        private SetterTeacherReviewSessionV1 _session;
        private string _message = "Run a training-lab rally, then refresh snapshots.";

        [MenuItem("Volleyball/AI/Review Setter Targets")]
        public static void Open()
        {
            GetWindow<SetterTeacherReviewWindowV1>("Setter Target Review");
        }

        private void OnGUI()
        {
            var snapshots = FindSnapshots();
            EditorGUILayout.LabelField("二传目标教师审核", EditorStyles.boldLabel);
            if (snapshots.Length == 0)
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
                if (GUILayout.Button("刷新")) Repaint();
                return;
            }

            _snapshotIndex = EditorGUILayout.Popup("冻结攻手目标", Mathf.Clamp(_snapshotIndex, 0, snapshots.Length - 1),
                snapshots.Select(snapshot => snapshot.SourceSequence + " · " + snapshot.SelectedAttacker.Value).ToArray());
            var snapshot = snapshots[_snapshotIndex];
            EditorGUILayout.LabelField("本地选择", snapshot.SelectedAttacker.Value);
            EditorGUILayout.LabelField("快照", snapshot.SnapshotHash.Substring(0, 12));
            foreach (var candidate in snapshot.Candidates.Where(value => value.IsFeasible))
                EditorGUILayout.LabelField(candidate.PlayerId.Value, "score " + candidate.Total.ToString("0.000"));

            if (_session == null || _session.Request.SnapshotHash != snapshot.SnapshotHash)
            {
                if (GUILayout.Button("请求教师排序")) Request(snapshot);
                EditorGUILayout.HelpBox(_message, MessageType.None);
                return;
            }

            var latest = _session.Attempts.LastOrDefault();
            if (latest == null)
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
                return;
            }

            if (!latest.IsSuccessful)
            {
                EditorGUILayout.HelpBox("教师调用失败: " + latest.Error, MessageType.Error);
                if (GUILayout.Button("重试")) Request(snapshot);
                return;
            }

            EditorGUILayout.LabelField("教师首选", latest.Response.TopChoice.Value);
            EditorGUILayout.LabelField("理由", latest.Response.Reason, EditorStyles.wordWrappedLabel);
            var candidates = _session.Request.Candidates;
            _acceptTeacher = EditorGUILayout.Toggle("接受教师首选", _acceptTeacher);
            if (!_acceptTeacher)
            {
                _selectedCandidateIndex = EditorGUILayout.Popup("纠正为",
                    Mathf.Clamp(_selectedCandidateIndex, 0, candidates.Count - 1),
                    candidates.Select(value => value.PlayerId.Value).ToArray());
                _correctionReason = EditorGUILayout.TextField("纠正原因", _correctionReason);
            }
            if (_session.ConfirmedReview == null && GUILayout.Button("确认标签"))
            {
                var selected = _acceptTeacher
                    ? latest.Response.TopChoice
                    : candidates[_selectedCandidateIndex].PlayerId;
                try
                {
                    var review = new SetterHumanReviewV1(
                        latest, selected, _acceptTeacher ? string.Empty : _correctionReason);
                    var path = new SetterLabelDatasetWriterV1().Append(new SetterLabelRecordV1(review));
                    _session.Confirm(review);
                    _message = "已确认并写入本地 JSONL: " + path;
                }
                catch (Exception exception)
                {
                    _message = "写入失败，可重试: " + exception.Message;
                }
                Repaint();
            }
            else if (_session.ConfirmedReview != null)
            {
                EditorGUILayout.HelpBox(_message, MessageType.Info);
            }
        }

        private async void Request(SetterTargetSnapshotV1 snapshot)
        {
            var config = MenShenBenchmarkConfiguration.Resolve(Environment.GetEnvironmentVariable);
            if (!config.CanRun)
            {
                _message = config.Error;
                Repaint();
                return;
            }

            _session = new SetterTeacherReviewSessionV1(
                new SetterTeacherReviewServiceV1(
                    new MenShenChatClient(new HttpClient(), config.Endpoint),
                    MenShenModelProfile.DoubaoMini,
                    config.ApiKey,
                    TimeSpan.FromSeconds(20)),
                SetterTeacherRequestV1.Create(snapshot));
            _message = "正在请求教师排序...";
            Repaint();
            var attempt = await _session.RequestAsync(CancellationToken.None);
            _message = attempt.IsSuccessful ? "教师排序已就绪。" : "教师调用失败。";
            Repaint();
        }

        private static SetterTargetSnapshotV1[] FindSnapshots()
        {
            var view = FindFirstObjectByType<TrainingScenarioLabView>();
            return view?.VisibleEvidence?.SetterTargets?.ToArray() ??
                   Array.Empty<SetterTargetSnapshotV1>();
        }
    }
}
