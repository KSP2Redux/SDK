using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP;
using Ksp2UnityTools.Editor.LinkedAddressables;
using Ksp2UnityTools.Editor.MissionAuthoring;
using Ksp2UnityTools.Editor.MissionAuthoring.Validation;
using Ksp2UnityTools.Editor.PartAuthoring.Validation;
using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;
using Ksp2UnityTools.Editor.TechTreeAuthoring;
using Ksp2UnityTools.Editor.TechTreeAuthoring.Validation;
using Ksp2UnityTools.Editor.Validation;
using Newtonsoft.Json.Linq;
using Redux.QA;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// An on-demand validator session shared by the editor menu and batch scripts.
    /// </summary>
    public class SharedValidatorRunner
    {
        private const string EXCEPTIONS_PATH = "Assets/Code/Redux/Editor/QA/ValidatorExceptions.json";
        private const string RUNNER_SUBJECT = "qa:runner";
        private const string PROJECT_KEY = "project";
        private const string CRASH_KEY = "crash";
        // Loaded prefabs are released every this many loads, so a full scan does not keep every model in memory.
        private const int UNLOAD_INTERVAL = 100;
        private readonly QaReport _report;
        private readonly Dictionary<string, (string Reason, string ApprovedBy)> _exceptions = new(StringComparer.Ordinal);
        private readonly HashSet<string> _matchedExceptions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _cleanRuns = new(StringComparer.Ordinal);
        private bool _hasFailures;
        private string[] _folders;
        private int _assetIndex;
        private int _assetCount;

        private SharedValidatorRunner(QaReport report, string[] folders)
        {
            _report = report;
            _folders = folders;
            // Names the checkout, so a later run on another checkout does not take this report as its baseline.
            _report.Add(new QaReportResult(folders == null ? RUNNER_SUBJECT : "qa:mod-runner", "Validator run", "Project", PROJECT_KEY, QaVerdict.Passed,
                ProjectPath));
        }

        /// <summary>
        /// Runs all validators using command-line options and exits only in batch mode.
        /// </summary>
        
        public static void RunAllValidators()
        {
            int exitCode;
            try
            {
                string[] arguments = Environment.GetCommandLineArgs();
                int option = Array.IndexOf(arguments, "-qaPreviousReport");
                if (option >= 0 && (option + 1 >= arguments.Length || arguments[option + 1].StartsWith("-", StringComparison.Ordinal)))
                    throw new ArgumentException("-qaPreviousReport requires auto, none or a report.json path.");

                exitCode = Run(option < 0 ? "auto" : arguments[option + 1]);
            }
            catch (Exception exception)
            {
                Debug.LogError($"QA validator runner could not write a report: {exception}");
                exitCode = 2;
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }

        /// <summary>
        /// Runs every authored content registry and prints the shared report path.
        /// </summary>
        /// <param name="previousJson">
        /// Auto to select the latest validator report, null or none for no comparison, or a report.json path.
        /// </param>
        /// <param name="folders">Optional mod content folders. Null scans the project.</param>
        /// <returns>Zero for completion without failures, one for failures, or two for an incomplete run.</returns>
        /// <exception cref="IOException">
        /// Thrown when the report cannot be written.
        /// </exception>
        public static int Run(string previousJson = "auto", string[] folders = null)
        {
            QaReport report;
            Exception previousError = null;
            try
            {
                report = new QaReport("All validators", "validator-run", PreviousReport(folders == null ? previousJson : "none"));
            }
            catch (Exception exception)
            {
                previousError = exception;
                report = new QaReport("All validators", "validator-run");
            }
            var runner = new SharedValidatorRunner(report, folders);
            if (folders != null)
                report.Add(new QaReportResult("qa:scope", "Mod content", "Folders", "scope", QaVerdict.Passed, string.Join("\n", folders)));
            bool isComplete = false;
            try
            {
                if (previousError != null)
                    runner.AddFailure("qa:runner", "Previous report", "load", previousError);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Stop play mode before running validators.");

                if (folders == null)
                    runner.LoadExceptions();
                runner.RunContent();
                isComplete = true;
            }
            catch (OperationCanceledException)
            {
                report.Add(new QaReportResult("qa:runner", "Validator run", "Completion", "status",
                    QaVerdict.NotTested, "Cancelled. Remaining content was not tested. Exception staleness was not checked."));
            }
            catch (Exception exception)
            {
                runner.AddFailure("qa:runner", "Completion", "crash", exception);
            }
            finally
            {
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();
            }
            runner.Finish(isComplete);
            string path = report.Write();
            Debug.Log($"QA validator report: {path}");
            return !isComplete ? 2 : runner._hasFailures ? 1 : 0;
        }

        private static string PreviousReport(string selection)
        {
            if (string.IsNullOrWhiteSpace(selection) || selection.Equals("none", StringComparison.OrdinalIgnoreCase))
                return null;
            if (!selection.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(selection);

            string folder = Path.Combine(Application.persistentDataPath, "QAReports", "validator-run");
            if (!Directory.Exists(folder))
                return null;

            // F5 directory names start with a UTC timestamp. Every Redux checkout on this machine shares the folder,
            // so the newest report counts only when it comes from this project and from a run that finished.
            return Directory.GetFiles(folder, "report.json", SearchOption.AllDirectories)
                .OrderByDescending(path => Path.GetFileName(Path.GetDirectoryName(path)), StringComparer.Ordinal)
                .FirstOrDefault(IsCompleteRunOfThisProject);
        }

        private static bool IsStockPart(GameObject prefab, CorePartData part)
        {
            if (!LinkedAddressableEditorIdentity.IsLinkedOrVariantOfLinked(prefab))
                return false;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
            CorePartData stock = source != null && source != prefab ? source.GetComponent<CorePartData>() : part;
            return stock == null || string.Equals(stock.Data?.partName, part.Data?.partName, StringComparison.Ordinal);
        }

        private static bool IsCompleteRunOfThisProject(string reportJson)
        {
            try
            {
                JObject report = JObject.Parse(File.ReadAllText(reportJson));
                var results = report["results"]?.OfType<JObject>().ToList() ?? new List<JObject>();
                bool isThisProject = results.Any(result => result.Value<string>("subjectId") == RUNNER_SUBJECT &&
                    result.Value<string>("key") == PROJECT_KEY && result.Value<string>("message") == ProjectPath);
                bool isComplete = results.Any(result => result.Value<string>("subjectId") == RUNNER_SUBJECT &&
                    result.Value<string>("check") == "Completion" && result.Value<string>("verdict") == nameof(QaVerdict.Passed));
                return isThisProject && isComplete && !results.Any(result => result.Value<string>("subjectId") == "qa:scope");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        private static string ProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private void LoadExceptions()
        {
            try
            {
                JArray entries = JArray.Parse(File.ReadAllText(EXCEPTIONS_PATH));
                foreach (JToken entry in entries)
                {
                    string id = entry.Value<string>("issueId");
                    string reason = entry.Value<string>("reason");
                    string approver = entry.Value<string>("approvedBy");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(approver))
                        throw new InvalidDataException("Each exception needs issueId, reason and approvedBy.");
                    if (!_exceptions.TryAdd(id, (reason, approver)))
                        throw new InvalidDataException($"Duplicate exception: {id}");
                }
            }
            catch (Exception exception)
            {
                // A malformed policy never applies a partial list of approvals.
                _exceptions.Clear();
                AddFailure("qa:runner", "Approved exceptions", "load", exception);
            }
        }

        private string[] FindAssets(string filter) => _folders == null ? AssetDatabase.FindAssets(filter)
            : _folders.Length == 0 ? Array.Empty<string>() : AssetDatabase.FindAssets(filter, _folders);

        private void RunContent()
        {
            string[] prefabs = FindAssets("t:GameObject").OrderBy(guid => guid, StringComparer.Ordinal).ToArray();
            string[] missions = FindAssets("t:Mission").OrderBy(guid => guid, StringComparer.Ordinal).ToArray();
            string[] trees = FindAssets("t:TechTreeAsset").OrderBy(guid => guid, StringComparer.Ordinal).ToArray();
            _assetCount = prefabs.Length + missions.Length + trees.Length;
            int parts = 0, bodies = 0, loaded = 0;
            var stockParts = new JArray();
            foreach (string guid in prefabs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Progress(path);
                // Released before the next load, never while a loaded prefab is in use.
                if (++loaded % UNLOAD_INTERVAL == 0)
                    EditorUtility.UnloadUnusedAssetsImmediate();

                try
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                        throw new InvalidDataException($"Cannot load GameObject asset {path}.");

                    // A linked stock prefab and a patch of one carry the stock part's data but are not authored
                    // parts, so they are listed apart and not validated as parts. A variant that renames the part
                    // is a new part built on a stock one, and it is validated.
                    CorePartData part = prefab.GetComponent<CorePartData>();
                    if (part != null && IsStockPart(prefab, part))
                    {
                        stockParts.Add(new JArray(path, part.Data?.partName ?? string.Empty));
                    }
                    else if (part != null)
                    {
                        parts++;
                        string subject = Subject("part", part.Data?.partName, guid);
                        RunValidators(subject, part.Data?.Title ?? part.name, new PartValidationContext(part),
                            ValidatorRegistry<PartValidationContext>.Validators, "Parts");
                    }
                    CoreCelestialBodyData body = prefab.GetComponent<CoreCelestialBodyData>();
                    if (body != null)
                    {
                        bodies++;
                        string subject = Subject("body", body.Data?.bodyName, guid);
                        BodyClassFlags bodyClass = BodyClassClassifier.Classify(body);
                        RunValidators(subject, body.Data?.bodyDisplayName ?? body.name, body,
                            ValidatorRegistry<CoreCelestialBodyData>.Validators.Where(validator =>
                                validator is not IPlanetValidator planet || (planet.AppliesTo & bodyClass) != 0), "Planets");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    AddFailure($"asset:{guid}", "Load content", "prefab", exception);
                }
            }
            RunAssets<Mission>(missions, "Missions", (mission, guid) =>
                RunValidators(Subject("mission", mission.missionData?.ID, guid), mission.missionData?.name ?? mission.name,
                    new MissionValidationContext(mission), ValidatorRegistry<MissionValidationContext>.Validators, "Missions"));
            RunAssets<TechTreeAsset>(trees, "Tech tree", (tree, guid) =>
            {
                var context = new TechTreeValidationContext(tree,
                    tree is BaseReferencedTechTreeAsset referenced ? TechTreeReference.Load(referenced) : null,
                    true, tree is TechTreePatchAsset patch ? TechTreeReference.LoadTreeData(patch) : null);
                string subject = Subject("tree", tree.name, guid);
                RunValidators(subject, tree.name, context, ValidatorRegistry<TechTreeValidationContext>.Validators, "Tech tree");
                var duplicateIds = new HashSet<string>(tree.Nodes.Where(node => !string.IsNullOrEmpty(node?.Data?.ID))
                    .GroupBy(node => node.Data.ID, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key),
                    StringComparer.Ordinal);
                for (int index = 0; index < tree.Nodes.Count; index++)
                {
                    TechTreeNode node = tree.Nodes[index];
                    // The window skips empty IDs. Here an index identifies malformed nodes so their checks still run.
                    string nodeId = string.IsNullOrEmpty(node?.Data?.ID) ? $"index:{index}" : node.Data.ID;
                    if (duplicateIds.Contains(nodeId))
                        nodeId = $"{nodeId}@index:{index}";
                    RunValidators($"{subject}/node:{nodeId}", node?.EnglishName ?? nodeId,
                        new TechNodeValidationContext(context, node, index),
                        ValidatorRegistry<TechNodeValidationContext>.Validators, "Tech nodes");
                }
            });
            AddCatalogCount("Parts", parts);
            var skipped = new QaReportResult(RUNNER_SUBJECT, "Validator run", "Linked stock parts", "skipped",
                QaVerdict.NotApplicable, $"Linked stock part prefabs and their patches skipped: {stockParts.Count}.");
            skipped.Evidence.Add(new JObject
            {
                ["type"] = "table", ["columns"] = new JArray("Prefab", "Part name"), ["rows"] = stockParts
            });
            _report.Add(skipped);
            AddCatalogCount("Planets", bodies);
            AddCatalogCount("Missions", missions.Length);
            AddCatalogCount("Tech tree", trees.Length);
        }

        private static string Subject(string domain, string id, string guid) => $"{domain}:{id}@{guid}";

        private void RunAssets<T>(string[] guids, string section, Action<T, string> run) where T : UnityEngine.Object
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Progress(path);
                try
                {
                    T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset == null)
                        throw new InvalidDataException($"Cannot load {section} asset {path}.");
                    run(asset, guid);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    AddFailure($"asset:{guid}", "Load content", section, exception);
                }
            }
        }

        private void RunValidators<T>(string subject, string name, T context, IEnumerable<IValidator<T>> validators, string section)
        {
            foreach (IValidator<T> validator in validators.OrderBy(value => value.GetType().FullName, StringComparer.Ordinal))
            {
                string check = validator.GetType().FullName;
                CheckCancellation($"{section}: {name} / {validator.GetType().Name}");
                var findings = new Dictionary<string, List<ValidationIssue>>(StringComparer.Ordinal);
                bool hasCrashed = false;
                try
                {
                    // Materialize within the catch because iterator bodies execute during enumeration.
                    foreach (ValidationIssue issue in validator.Validate(context) ?? Array.Empty<ValidationIssue>())
                    {
                        string key = string.IsNullOrEmpty(issue.Code) ? "finding" : issue.Code;
                        if (!findings.TryGetValue(key, out List<ValidationIssue> group))
                        {
                            group = new List<ValidationIssue>();
                            findings.Add(key, group);
                        }
                        group.Add(issue);
                    }
                }
                catch (Exception exception)
                {
                    hasCrashed = true;
                    AddFailure(subject, check, CRASH_KEY, exception);
                }
                foreach (KeyValuePair<string, List<ValidationIssue>> finding in findings)
                {
                    ValidationSeverity severity = finding.Value.Max(issue => issue.Severity);
                    QaVerdict verdict = severity switch
                    {
                        ValidationSeverity.Error => QaVerdict.Failed,
                        ValidationSeverity.Warning => QaVerdict.NeedsReview,
                        _ => QaVerdict.Passed
                    };
                    var result = new QaReportResult(subject, name, check, finding.Key, verdict,
                        string.Join("\n", finding.Value.Select(issue => issue.Message)));
                    AddFinding(result);
                }
                if (hasCrashed || findings.Count != 0)
                    continue;

                _cleanRuns.TryGetValue(section, out int count);
                _cleanRuns[section] = count + 1;
            }
        }

        private void AddFailure(string subject, string check, string key, Exception exception)
        {
            var result = new QaReportResult(subject, subject, check, key, QaVerdict.Failed, exception.Message);
            result.Evidence.Add(new JObject { ["type"] = "log", ["text"] = exception.ToString() });
            AddFinding(result);
        }

        private void AddFinding(QaReportResult result)
        {
            // A crashed validator or a runner problem means the content was not checked, so no approval can hide it.
            bool isApprovable = result.Key != CRASH_KEY && result.SubjectId != RUNNER_SUBJECT;
            if (isApprovable && (result.Verdict is QaVerdict.Failed or QaVerdict.NeedsReview) &&
                _exceptions.TryGetValue(result.IssueId, out var approval))
            {
                _matchedExceptions.Add(result.IssueId);
                result.Verdict = QaVerdict.NotApplicable;
                result.Message = $"Approved by {approval.ApprovedBy}: {approval.Reason}\n{result.Message}";
            }
            _hasFailures |= result.Verdict == QaVerdict.Failed;
            _report.Add(result);
        }

        private void Progress(string path)
        {
            CheckCancellation(path);
            _assetIndex++;
        }

        private void CheckCancellation(string message)
        {
            if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("QA runs - All validators", message,
                    _assetCount == 0 ? 0f : (float)_assetIndex / _assetCount))
                throw new OperationCanceledException();
        }

        private void AddCatalogCount(string section, int count)
        {
            if (count == 0 && _folders == null)
                _hasFailures = true;
            _report.Add(new QaReportResult("qa:runner", "Validator run", section, "catalog",
                count == 0 ? (_folders == null ? QaVerdict.NotTested : QaVerdict.NotApplicable) : QaVerdict.Passed, $"Authored content assets scanned: {count}."));
        }

        private void Finish(bool isComplete)
        {
            foreach (KeyValuePair<string, int> section in _cleanRuns)
            {
                _report.Add(new QaReportResult("qa:runner", "Validator run", section.Key, "clean",
                    QaVerdict.Passed, $"{section.Value} subject and validator executions returned no findings."));
            }
            if (!isComplete)
                return;

            foreach (var approval in _exceptions)
            {
                if (_matchedExceptions.Contains(approval.Key))
                    continue;
                _report.Add(new QaReportResult("qa:exceptions", "Approved exceptions", "Unused exception", approval.Key,
                    QaVerdict.NeedsReview, $"Unknown or stale issue ID: {approval.Key}\nApproved by {approval.Value.ApprovedBy}: {approval.Value.Reason}"));
            }
            // The row says the run is complete, so a complete run passes even when its content has failures. Those
            // failures have their own rows and set the exit code.
            _report.Add(new QaReportResult("qa:runner", "Validator run", "Completion", "status", QaVerdict.Passed,
                _hasFailures
                    ? "All discovered authored content was scanned. Some content failed validation."
                    : "All discovered authored content was scanned. No failures."));
        }
    }
}
