using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Volleyball.Career.Domain
{
    public enum TryoutOutputKind
    {
        Spike = 0,
        Serve = 1,
        Reception = 2,
        Defense = 3,
        Block = 4,
        Movement = 5,
        Jump = 6,
        Stamina = 7,
        Fatigue = 8,
        Mindset = 9,
        CoachTrust = 10
    }

    public sealed class TryoutOutputDefinition
    {
        public TryoutOutputDefinition(string outputId, TryoutOutputKind kind)
        {
            OutputId = CareerSaveModelGuard.BusinessId(outputId, nameof(outputId));
            CareerSaveModelGuard.DefinedEnum(kind, nameof(kind));
            Kind = kind;
        }

        public string OutputId { get; }

        public TryoutOutputKind Kind { get; }
    }

    public sealed class TryoutChoiceDefinition
    {
        private readonly int[] _baseValues;
        private readonly ReadOnlyCollection<int> _readOnlyBaseValues;

        public TryoutChoiceDefinition(string choiceId, IEnumerable<int> baseValues)
        {
            ChoiceId = CareerSaveModelGuard.BusinessId(choiceId, nameof(choiceId));
            if (baseValues == null)
            {
                throw new ArgumentNullException(nameof(baseValues));
            }

            var values = new List<int>();
            foreach (var value in baseValues)
            {
                values.Add(value);
            }

            if (values.Count == 0)
            {
                throw new ArgumentException("A tryout choice requires output values.", nameof(baseValues));
            }

            _baseValues = values.ToArray();
            _readOnlyBaseValues = Array.AsReadOnly(_baseValues);
        }

        public string ChoiceId { get; }

        public IReadOnlyList<int> BaseValues => _readOnlyBaseValues;
    }

    public sealed class TryoutStageDefinition
    {
        private readonly TryoutOutputDefinition[] _outputs;
        private readonly TryoutChoiceDefinition[] _choices;
        private readonly ReadOnlyCollection<TryoutOutputDefinition> _readOnlyOutputs;
        private readonly ReadOnlyCollection<TryoutChoiceDefinition> _readOnlyChoices;

        public TryoutStageDefinition(
            int stageNumber,
            string stageId,
            IEnumerable<TryoutOutputDefinition> outputs,
            IEnumerable<TryoutChoiceDefinition> choices)
        {
            StageNumber = CareerSaveModelGuard.InclusiveRange(
                stageNumber,
                1,
                3,
                nameof(stageNumber));
            StageId = CareerSaveModelGuard.BusinessId(stageId, nameof(stageId));
            _outputs = CopyOutputs(outputs);
            _choices = CopyChoices(choices, _outputs.Length);
            _readOnlyOutputs = Array.AsReadOnly(_outputs);
            _readOnlyChoices = Array.AsReadOnly(_choices);
        }

        public int StageNumber { get; }

        public string StageId { get; }

        public IReadOnlyList<TryoutOutputDefinition> Outputs => _readOnlyOutputs;

        public IReadOnlyList<TryoutChoiceDefinition> Choices => _readOnlyChoices;

        public TryoutChoiceDefinition FindChoice(string choiceId)
        {
            for (var index = 0; index < _choices.Length; index++)
            {
                if (string.Equals(_choices[index].ChoiceId, choiceId, StringComparison.Ordinal))
                {
                    return _choices[index];
                }
            }

            return null;
        }

        private static TryoutOutputDefinition[] CopyOutputs(
            IEnumerable<TryoutOutputDefinition> outputs)
        {
            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            var result = new List<TryoutOutputDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in outputs)
            {
                if (output == null || !ids.Add(output.OutputId))
                {
                    throw new ArgumentException(
                        "Stage outputs must be non-null and unique.",
                        nameof(outputs));
                }

                result.Add(new TryoutOutputDefinition(output.OutputId, output.Kind));
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("A tryout stage requires outputs.", nameof(outputs));
            }

            return result.ToArray();
        }

        private static TryoutChoiceDefinition[] CopyChoices(
            IEnumerable<TryoutChoiceDefinition> choices,
            int outputCount)
        {
            if (choices == null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            var result = new List<TryoutChoiceDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in choices)
            {
                if (choice == null || !ids.Add(choice.ChoiceId) ||
                    choice.BaseValues.Count != outputCount)
                {
                    throw new ArgumentException(
                        "Stage choices must be non-null, unique, and match the output count.",
                        nameof(choices));
                }

                result.Add(new TryoutChoiceDefinition(choice.ChoiceId, choice.BaseValues));
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("A tryout stage requires choices.", nameof(choices));
            }

            return result.ToArray();
        }
    }

    public sealed class TryoutCatalog
    {
        private readonly TryoutStageDefinition[] _stages;
        private readonly ReadOnlyCollection<TryoutStageDefinition> _readOnlyStages;

        public TryoutCatalog(
            int contentVersion,
            int rulesetVersion,
            string initialTeamStableId,
            IEnumerable<TryoutStageDefinition> stages)
        {
            if (contentVersion != 1 || rulesetVersion != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contentVersion),
                    "Only tryout content/rules V1 is supported.");
            }

            if (string.IsNullOrWhiteSpace(initialTeamStableId))
            {
                throw new ArgumentException(
                    "A stable initial team ID is required.",
                    nameof(initialTeamStableId));
            }

            if (!string.Equals(
                initialTeamStableId,
                "team.university.first",
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Tryout content V1 requires its registered initial team ID.",
                    nameof(initialTeamStableId));
            }

            if (stages == null)
            {
                throw new ArgumentNullException(nameof(stages));
            }

            var copied = new List<TryoutStageDefinition>();
            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            var choiceIds = new HashSet<string>(StringComparer.Ordinal);
            var outputIds = new HashSet<string>(StringComparer.Ordinal);
            var outputKinds = new HashSet<TryoutOutputKind>();
            foreach (var stage in stages)
            {
                if (stage == null || stage.StageNumber != copied.Count + 1 ||
                    !stageIds.Add(stage.StageId))
                {
                    throw new ArgumentException(
                        "Tryout stages must be non-null, unique, and ordered 1 through 3.",
                        nameof(stages));
                }

                foreach (var choice in stage.Choices)
                {
                    if (!choiceIds.Add(choice.ChoiceId))
                    {
                        throw new ArgumentException(
                            "Tryout choice IDs must be globally unique.",
                            nameof(stages));
                    }
                }

                foreach (var output in stage.Outputs)
                {
                    if (!outputIds.Add(output.OutputId) || !outputKinds.Add(output.Kind))
                    {
                        throw new ArgumentException(
                            "Tryout output IDs and semantics must be globally unique.",
                            nameof(stages));
                    }
                }

                copied.Add(new TryoutStageDefinition(
                    stage.StageNumber,
                    stage.StageId,
                    stage.Outputs,
                    stage.Choices));
            }

            if (copied.Count != 3 || outputKinds.Count != 11)
            {
                throw new ArgumentException(
                    "Tryout content V1 requires exactly three stages and eleven output semantics.",
                    nameof(stages));
            }

            ValidateV1StageShape(copied);

            ContentVersion = contentVersion;
            RulesetVersion = rulesetVersion;
            InitialTeamStableId = CareerSaveModelGuard.BusinessId(
                initialTeamStableId,
                nameof(initialTeamStableId));
            _stages = copied.ToArray();
            _readOnlyStages = Array.AsReadOnly(_stages);
        }

        public int ContentVersion { get; }

        public int RulesetVersion { get; }

        public string InitialTeamStableId { get; }

        public IReadOnlyList<TryoutStageDefinition> Stages => _readOnlyStages;

        public TryoutStageDefinition GetStage(int stageNumber)
        {
            if (stageNumber < 1 || stageNumber > _stages.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stageNumber));
            }

            return _stages[stageNumber - 1];
        }

        private static void ValidateV1StageShape(
            IReadOnlyList<TryoutStageDefinition> stages)
        {
            var expectedStageIds = new[]
            {
                "tryout.attack",
                "tryout.reception_defense",
                "tryout.scrimmage"
            };
            var expectedOutputIds = new[]
            {
                new[]
                {
                    "tryout.output.spike",
                    "tryout.output.serve",
                    "tryout.output.jump"
                },
                new[]
                {
                    "tryout.output.reception",
                    "tryout.output.defense",
                    "tryout.output.block",
                    "tryout.output.movement"
                },
                new[]
                {
                    "tryout.output.stamina",
                    "tryout.output.fatigue",
                    "tryout.output.mindset",
                    "tryout.output.coach_trust"
                }
            };
            var expectedOutputKinds = new[]
            {
                new[]
                {
                    TryoutOutputKind.Spike,
                    TryoutOutputKind.Serve,
                    TryoutOutputKind.Jump
                },
                new[]
                {
                    TryoutOutputKind.Reception,
                    TryoutOutputKind.Defense,
                    TryoutOutputKind.Block,
                    TryoutOutputKind.Movement
                },
                new[]
                {
                    TryoutOutputKind.Stamina,
                    TryoutOutputKind.Fatigue,
                    TryoutOutputKind.Mindset,
                    TryoutOutputKind.CoachTrust
                }
            };
            var expectedChoiceIds = new[]
            {
                new[]
                {
                    "tryout.attack.choice.power",
                    "tryout.attack.choice.serve",
                    "tryout.attack.choice.approach"
                },
                new[]
                {
                    "tryout.reception_defense.choice.first_touch",
                    "tryout.reception_defense.choice.floor_defense",
                    "tryout.reception_defense.choice.net_read"
                },
                new[]
                {
                    "tryout.scrimmage.choice.endurance",
                    "tryout.scrimmage.choice.composure",
                    "tryout.scrimmage.choice.initiative"
                }
            };
            var expectedBaseValues = new[]
            {
                new[]
                {
                    new[] { 5800, 4800, 5600 },
                    new[] { 5000, 5800, 5100 },
                    new[] { 5400, 5100, 5400 }
                },
                new[]
                {
                    new[] { 5800, 5200, 4600, 5300 },
                    new[] { 5100, 5800, 4600, 5500 },
                    new[] { 5000, 5100, 5700, 5400 }
                },
                new[]
                {
                    new[] { 5800, 8, 52, 48 },
                    new[] { 5200, 10, 60, 56 },
                    new[] { 5400, 14, 56, 60 }
                }
            };
            for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                var stage = stages[stageIndex];
                if (!string.Equals(
                    stage.StageId,
                    expectedStageIds[stageIndex],
                    StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Tryout content V1 stage IDs must match the registered order.",
                        nameof(stages));
                }

                if (stage.Choices.Count != 3 ||
                    stage.Outputs.Count != expectedOutputKinds[stageIndex].Length)
                {
                    throw new ArgumentException(
                        "Tryout content V1 requires three choices and its exact output count per stage.",
                        nameof(stages));
                }

                for (var outputIndex = 0; outputIndex < stage.Outputs.Count; outputIndex++)
                {
                    if (!string.Equals(
                            stage.Outputs[outputIndex].OutputId,
                            expectedOutputIds[stageIndex][outputIndex],
                            StringComparison.Ordinal) ||
                        stage.Outputs[outputIndex].Kind !=
                        expectedOutputKinds[stageIndex][outputIndex])
                    {
                        throw new ArgumentException(
                            "Tryout content V1 output IDs and semantics must match the registered stage order.",
                            nameof(stages));
                    }
                }

                for (var choiceIndex = 0; choiceIndex < stage.Choices.Count; choiceIndex++)
                {
                    var choice = stage.Choices[choiceIndex];
                    if (!string.Equals(
                        choice.ChoiceId,
                        expectedChoiceIds[stageIndex][choiceIndex],
                        StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "Tryout content V1 choice IDs must match the registered order.",
                            nameof(stages));
                    }

                    for (var valueIndex = 0; valueIndex < choice.BaseValues.Count; valueIndex++)
                    {
                        if (choice.BaseValues[valueIndex] !=
                            expectedBaseValues[stageIndex][choiceIndex][valueIndex])
                        {
                            throw new ArgumentException(
                                "Tryout content V1 base tuning must match the registered fixture.",
                                nameof(stages));
                        }
                    }
                }
            }
        }
    }

    public static class TryoutCatalogV1
    {
        public static TryoutCatalog Create()
        {
            return new TryoutCatalog(
                1,
                1,
                "team.university.first",
                new[]
                {
                    Stage(
                        1,
                        "tryout.attack",
                        new[]
                        {
                            Output("tryout.output.spike", TryoutOutputKind.Spike),
                            Output("tryout.output.serve", TryoutOutputKind.Serve),
                            Output("tryout.output.jump", TryoutOutputKind.Jump)
                        },
                        Choice("tryout.attack.choice.power", 5800, 4800, 5600),
                        Choice("tryout.attack.choice.serve", 5000, 5800, 5100),
                        Choice("tryout.attack.choice.approach", 5400, 5100, 5400)),
                    Stage(
                        2,
                        "tryout.reception_defense",
                        new[]
                        {
                            Output("tryout.output.reception", TryoutOutputKind.Reception),
                            Output("tryout.output.defense", TryoutOutputKind.Defense),
                            Output("tryout.output.block", TryoutOutputKind.Block),
                            Output("tryout.output.movement", TryoutOutputKind.Movement)
                        },
                        Choice("tryout.reception_defense.choice.first_touch", 5800, 5200, 4600, 5300),
                        Choice("tryout.reception_defense.choice.floor_defense", 5100, 5800, 4600, 5500),
                        Choice("tryout.reception_defense.choice.net_read", 5000, 5100, 5700, 5400)),
                    Stage(
                        3,
                        "tryout.scrimmage",
                        new[]
                        {
                            Output("tryout.output.stamina", TryoutOutputKind.Stamina),
                            Output("tryout.output.fatigue", TryoutOutputKind.Fatigue),
                            Output("tryout.output.mindset", TryoutOutputKind.Mindset),
                            Output("tryout.output.coach_trust", TryoutOutputKind.CoachTrust)
                        },
                        Choice("tryout.scrimmage.choice.endurance", 5800, 8, 52, 48),
                        Choice("tryout.scrimmage.choice.composure", 5200, 10, 60, 56),
                        Choice("tryout.scrimmage.choice.initiative", 5400, 14, 56, 60))
                });
        }

        private static TryoutStageDefinition Stage(
            int number,
            string id,
            TryoutOutputDefinition[] outputs,
            params TryoutChoiceDefinition[] choices)
        {
            return new TryoutStageDefinition(number, id, outputs, choices);
        }

        private static TryoutOutputDefinition Output(string id, TryoutOutputKind kind)
        {
            return new TryoutOutputDefinition(id, kind);
        }

        private static TryoutChoiceDefinition Choice(string id, params int[] values)
        {
            return new TryoutChoiceDefinition(id, values);
        }
    }
}
