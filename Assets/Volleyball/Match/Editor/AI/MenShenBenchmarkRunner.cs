using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volleyball.AI;

namespace Volleyball.Editor.AI
{
    public sealed class MenShenBenchmarkRunner
    {
        private const string SystemPrompt =
            "You are a volleyball decision engine. Return only the requested JSON object.";

        private readonly IMenShenChatClient client;
        private readonly IReadOnlyList<MenShenModelProfile> profiles;
        private readonly string apiKey;
        private readonly TimeSpan pacingInterval;

        public MenShenBenchmarkRunner(
            IMenShenChatClient client,
            IReadOnlyList<MenShenModelProfile> profiles,
            string apiKey,
            TimeSpan pacingInterval)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            if (profiles.Count != 3)
            {
                throw new ArgumentException("Benchmark requires exactly three profiles.", nameof(profiles));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required.", nameof(apiKey));
            }

            if (pacingInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(pacingInterval));
            }

            this.apiKey = apiKey;
            this.pacingInterval = pacingInterval;
        }

        public async Task<MenShenBenchmarkRunResult> RunAsync(
            BenchmarkCaseCatalog catalog,
            int repetitions,
            int seed,
            CancellationToken cancellationToken)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (repetitions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            var plans = CreatePlans(catalog, repetitions, seed);
            var attempts = new List<MenShenBenchmarkAttempt>(plans.Count);
            foreach (var plan in plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var casePrompt = BenchmarkPromptBuilder.Build(plan.Case);
                var result = await client.CompleteAsync(
                    plan.Profile,
                    SystemPrompt,
                    casePrompt,
                    apiKey,
                    TimeSpan.FromMilliseconds(plan.Case.DeadlineMilliseconds),
                    cancellationToken).ConfigureAwait(false);

                attempts.Add(Evaluate(plan, casePrompt, result));
                if (pacingInterval > TimeSpan.Zero)
                {
                    await Task.Delay(pacingInterval, cancellationToken).ConfigureAwait(false);
                }
            }

            return new MenShenBenchmarkRunResult(
                Array.AsReadOnly(attempts.ToArray()),
                CreateAliasMap(seed));
        }

        private List<AttemptPlan> CreatePlans(
            BenchmarkCaseCatalog catalog,
            int repetitions,
            int seed)
        {
            var plans = new List<AttemptPlan>(profiles.Count * catalog.Cases.Count * repetitions);
            for (var repetition = 1; repetition <= repetitions; repetition++)
            {
                foreach (var profile in profiles)
                {
                    foreach (var item in catalog.Cases)
                    {
                        plans.Add(new AttemptPlan(profile, item, repetition));
                    }
                }
            }

            var random = new Random(seed);
            for (var index = plans.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                var current = plans[index];
                plans[index] = plans[swapIndex];
                plans[swapIndex] = current;
            }

            return plans;
        }

        private IReadOnlyDictionary<string, string> CreateAliasMap(int seed)
        {
            var modelIds = profiles.Select(profile => profile.ModelId).ToArray();
            var random = new Random(unchecked(seed ^ 0x5A17A11A));
            for (var index = modelIds.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                var current = modelIds[index];
                modelIds[index] = modelIds[swapIndex];
                modelIds[swapIndex] = current;
            }

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["A"] = modelIds[0],
                ["B"] = modelIds[1],
                ["C"] = modelIds[2]
            };
        }

        private static MenShenBenchmarkAttempt Evaluate(
            AttemptPlan plan,
            string casePrompt,
            MenShenChatResult result)
        {
            var formatScore = 0;
            var preferredScore = 0;
            var usedRepair = false;
            var parsedDecisionJson = string.Empty;
            var hardZeroReasons = new List<string>();

            if (result.Status == MenShenChatStatus.Success)
            {
                try
                {
                    parsedDecisionJson = ParseAndNormalize(plan.Case, result.Content);
                }
                catch (DecisionFormatException)
                {
                    if (DecisionJsonRepair.TryStripSingleMarkdownFence(result.Content, out var repaired))
                    {
                        try
                        {
                            parsedDecisionJson = ParseAndNormalize(plan.Case, repaired);
                            usedRepair = true;
                        }
                        catch (DecisionFormatException)
                        {
                            hardZeroReasons.Add("schema-failure");
                        }
                    }
                    else
                    {
                        hardZeroReasons.Add("schema-failure");
                    }
                }

                if (!string.IsNullOrEmpty(parsedDecisionJson))
                {
                    formatScore = 2;
                    if (parsedDecisionJson == plan.Case.PreferredJson)
                    {
                        preferredScore = 1;
                    }

                    if (plan.Case.Kind == BenchmarkCaseKind.Touch &&
                        !TouchDecisionRules.Validate(
                            DecisionJsonCodec.ParseTouch(parsedDecisionJson),
                            plan.Case.CountedTeamTouches).IsValid)
                    {
                        hardZeroReasons.Add("third-touch-not-over-net");
                    }
                }
            }

            return new MenShenBenchmarkAttempt(
                plan.Profile.ModelId,
                plan.Case.Id,
                plan.Case.Kind,
                plan.Repetition,
                plan.Case.DeadlineMilliseconds,
                plan.Case.CountedTeamTouches,
                casePrompt,
                plan.Case.PreferredJson,
                result,
                usedRepair,
                parsedDecisionJson,
                formatScore,
                preferredScore,
                Array.AsReadOnly(hardZeroReasons.ToArray()));
        }

        private static string ParseAndNormalize(BenchmarkCase item, string content)
        {
            if (item.Kind == BenchmarkCaseKind.Round)
            {
                var decision = DecisionJsonCodec.ParseRound(content);
                return DecisionJsonFormatter.Format(decision);
            }

            var touch = DecisionJsonCodec.ParseTouch(content);
            return DecisionJsonFormatter.Format(touch);
        }

        private readonly struct AttemptPlan
        {
            public AttemptPlan(MenShenModelProfile profile, BenchmarkCase item, int repetition)
            {
                Profile = profile;
                Case = item;
                Repetition = repetition;
            }

            public MenShenModelProfile Profile { get; }

            public BenchmarkCase Case { get; }

            public int Repetition { get; }
        }
    }

    public sealed class MenShenBenchmarkRunResult
    {
        public MenShenBenchmarkRunResult(
            IReadOnlyList<MenShenBenchmarkAttempt> attempts,
            IReadOnlyDictionary<string, string> aliasToModel)
        {
            Attempts = attempts;
            AliasToModel = aliasToModel;
        }

        public IReadOnlyList<MenShenBenchmarkAttempt> Attempts { get; }

        public IReadOnlyDictionary<string, string> AliasToModel { get; }
    }

    public sealed class MenShenBenchmarkAttempt
    {
        public MenShenBenchmarkAttempt(
            string modelId,
            string caseId,
            BenchmarkCaseKind kind,
            int repetition,
            int deadlineMilliseconds,
            int countedTeamTouches,
            string casePrompt,
            string preferredJson,
            MenShenChatResult chatResult,
            bool usedMarkdownFenceRepair,
            string parsedDecisionJson,
            int formatScore,
            int preferredMatchScore,
            IReadOnlyList<string> hardZeroReasons)
        {
            ModelId = modelId;
            CaseId = caseId;
            Kind = kind;
            Repetition = repetition;
            DeadlineMilliseconds = deadlineMilliseconds;
            CountedTeamTouches = countedTeamTouches;
            CasePrompt = casePrompt;
            PreferredJson = preferredJson;
            ChatResult = chatResult;
            UsedMarkdownFenceRepair = usedMarkdownFenceRepair;
            ParsedDecisionJson = parsedDecisionJson;
            FormatScore = formatScore;
            PreferredMatchScore = preferredMatchScore;
            HardZeroReasons = hardZeroReasons;
        }

        public string ModelId { get; }

        public string CaseId { get; }

        public BenchmarkCaseKind Kind { get; }

        public int Repetition { get; }

        public int DeadlineMilliseconds { get; }

        public int CountedTeamTouches { get; }

        public string CasePrompt { get; }

        public string PreferredJson { get; }

        public MenShenChatResult ChatResult { get; }

        public MenShenChatStatus Status => ChatResult.Status;

        public bool UsedMarkdownFenceRepair { get; }

        public string ParsedDecisionJson { get; }

        public int FormatScore { get; }

        public int PreferredMatchScore { get; }

        public IReadOnlyList<string> HardZeroReasons { get; }
    }

    public static class DecisionJsonFormatter
    {
        public static string Format(RoundDecisionV1 decision)
        {
            return "{\"receiver\":\"" + FormatRole(decision.Receiver) +
                   "\",\"second_actor\":\"" + FormatRole(decision.SecondActor) +
                   "\",\"set_route\":\"" + FormatSetRoute(decision.SetRoute) +
                   "\",\"third_actor\":\"" + FormatRole(decision.ThirdActor) +
                   "\",\"attack_route\":\"" + FormatSpikeRoute(decision.AttackRoute) + "\"}";
        }

        public static string Format(TouchDecisionV1 decision)
        {
            return "{\"next_actor\":\"" + FormatRole(decision.NextActor) +
                   "\",\"action\":\"" + FormatAction(decision.Action) +
                   "\",\"target_zone\":\"" + FormatTargetZone(decision.TargetZone) +
                   "\",\"tempo\":\"" + FormatTempo(decision.Tempo) +
                   "\",\"risk\":\"" + FormatRisk(decision.Risk) + "\"}";
        }

        private static string FormatRole(Volleyball.Domain.Prototype.PlayerRole role)
        {
            switch (role)
            {
                case Volleyball.Domain.Prototype.PlayerRole.Defender:
                    return "defender";
                case Volleyball.Domain.Prototype.PlayerRole.Setter:
                    return "setter";
                case Volleyball.Domain.Prototype.PlayerRole.Attacker:
                    return "attacker";
                default:
                    throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        private static string FormatSetRoute(SetRoute route)
        {
            switch (route)
            {
                case SetRoute.LeftPin:
                    return "left_pin";
                case SetRoute.MiddleQuick:
                    return "middle_quick";
                case SetRoute.RightPin:
                    return "right_pin";
                case SetRoute.BackSet:
                    return "back_set";
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private static string FormatSpikeRoute(SpikeRoute route)
        {
            switch (route)
            {
                case SpikeRoute.Line:
                    return "line";
                case SpikeRoute.CrossCourt:
                    return "cross_court";
                case SpikeRoute.DeepSeam:
                    return "deep_seam";
                case SpikeRoute.RollShot:
                    return "roll_shot";
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private static string FormatAction(TouchDecisionAction action)
        {
            switch (action)
            {
                case TouchDecisionAction.Receive:
                    return "receive";
                case TouchDecisionAction.Set:
                    return "set";
                case TouchDecisionAction.Attack:
                    return "attack";
                case TouchDecisionAction.FreeBall:
                    return "free_ball";
                case TouchDecisionAction.EmergencySave:
                    return "emergency_save";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static string FormatTargetZone(TargetZone zone)
        {
            switch (zone)
            {
                case TargetZone.LeftFront:
                    return "left_front";
                case TargetZone.MiddleFront:
                    return "middle_front";
                case TargetZone.RightFront:
                    return "right_front";
                case TargetZone.LeftBack:
                    return "left_back";
                case TargetZone.MiddleBack:
                    return "middle_back";
                case TargetZone.RightBack:
                    return "right_back";
                default:
                    throw new ArgumentOutOfRangeException(nameof(zone));
            }
        }

        private static string FormatTempo(DecisionTempo tempo)
        {
            switch (tempo)
            {
                case DecisionTempo.Quick:
                    return "quick";
                case DecisionTempo.Normal:
                    return "normal";
                case DecisionTempo.High:
                    return "high";
                default:
                    throw new ArgumentOutOfRangeException(nameof(tempo));
            }
        }

        private static string FormatRisk(DecisionRisk risk)
        {
            switch (risk)
            {
                case DecisionRisk.Safe:
                    return "safe";
                case DecisionRisk.Balanced:
                    return "balanced";
                case DecisionRisk.Aggressive:
                    return "aggressive";
                default:
                    throw new ArgumentOutOfRangeException(nameof(risk));
            }
        }
    }
}
