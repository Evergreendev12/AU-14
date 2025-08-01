using System.Linq;
using System.Text;
using Content.Server.Popups;
using Content.Shared.UserInterface;
using Content.Shared.DoAfter;
using Content.Shared.Forensics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Humanoid;
using Content.Shared.StationRecords;
using Robust.Shared.Prototypes;

// todo: remove this stinky LINQy

namespace Content.Server.Forensics.Systems
{
    public sealed class ForensicScannerSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly PaperSystem _paperSystem = default!;
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly ForensicsSystem _forensicsSystem = default!;
        [Dependency] private readonly TagSystem _tag = default!;
        [Dependency] private readonly CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

        private static readonly ProtoId<TagPrototype> DnaSolutionScannableTag = "DNASolutionScannable";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ForensicScannerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<ForensicScannerComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
            SubscribeLocalEvent<ForensicScannerComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
            SubscribeLocalEvent<ForensicScannerComponent, GetVerbsEvent<UtilityVerb>>(OnUtilityVerb);
            SubscribeLocalEvent<ForensicScannerComponent, ForensicScannerPrintMessage>(OnPrint);
            SubscribeLocalEvent<ForensicScannerComponent, ForensicScannerClearMessage>(OnClear);
            SubscribeLocalEvent<ForensicScannerComponent, ForensicScannerDoAfterEvent>(OnDoAfter);
        }

        private void UpdateUserInterface(EntityUid uid, ForensicScannerComponent component)
        {
            var state = new ForensicScannerBoundUserInterfaceState(
                component.Fingerprints,
                component.Fibers,
                component.TouchDNAs,
                component.SolutionDNAs,
                component.Residues,
                component.LastScannedName,
                component.PrintCooldown,
                component.PrintReadyAt);

            _uiSystem.SetUiState(uid, ForensicScannerUiKey.Key, state);
        }

        private void OnDoAfter(EntityUid uid, ForensicScannerComponent component, DoAfterEvent args)
        {
            if (args.Handled || args.Cancelled)
                return;

            if (!TryComp(uid, out ForensicScannerComponent? scanner))
                return;

            if (args.Args.Target != null)
            {
                if (!TryComp<ForensicsComponent>(args.Args.Target, out var forensics))
                {
                    scanner.Fingerprints = new();
                    scanner.Fibers = new();
                    scanner.TouchDNAs = new();
                    scanner.Residues = new();
                }
                else
                {
                    scanner.Fingerprints = forensics.Fingerprints.ToList();
                    scanner.Fibers = forensics.Fibers.ToList();
                    scanner.TouchDNAs = forensics.DNAs.ToList();
                    scanner.Residues = forensics.Residues.ToList();
                }

                if (_tag.HasTag(args.Args.Target.Value, DnaSolutionScannableTag))
                {
                    scanner.SolutionDNAs = _forensicsSystem.GetSolutionsDNA(args.Args.Target.Value);
                }
                else
                {
                    scanner.SolutionDNAs = new();
                }

                scanner.LastScannedName = MetaData(args.Args.Target.Value).EntityName;

                // Only add a record if the last thing scanned was a humanoid
                if (!TryComp(args.Args.Target.Value, out HumanoidAppearanceComponent? _))
                    return;

                // Try to get DNA and fingerprint directly from components if present
                string? dnaValue = null;
                string? fingerprintValue = null;
                if (TryComp<Content.Shared.Forensics.Components.DnaComponent>(args.Args.Target.Value, out var dnaComp) && !string.IsNullOrEmpty(dnaComp.DNA))
                {
                    dnaValue = dnaComp.DNA;
                }
                if (TryComp<Content.Shared.Forensics.Components.FingerprintComponent>(args.Args.Target.Value, out var fpComp) && !string.IsNullOrEmpty(fpComp.Fingerprint))
                {
                    fingerprintValue = fpComp.Fingerprint;
                }

                // Add a general record if not present (required for criminal record)
                var station = _stationSystem.GetOwningStation(uid);
                if (station == null)
                    return;
                var generalKey = _stationRecords.GetRecordByName(station.Value, scanner.LastScannedName);
                StationRecordKey key;
                if (generalKey is not uint id)
                {
                    key = _stationRecords.AddRecordEntry(station.Value, new GeneralStationRecord
                    {
                        Name = scanner.LastScannedName,
                        // Prefer component DNA/fingerprint, fallback to scanned evidence, then N/A
                        DNA = dnaValue ?? (scanner.TouchDNAs.Count > 0 ? string.Join(", ", scanner.TouchDNAs) : "N/A"),
                        Fingerprint = fingerprintValue ?? (scanner.Fingerprints.Count > 0 ? string.Join(", ", scanner.Fingerprints) : "N/A")
                    });
                }
                else
                {
                    key = new StationRecordKey(id, station.Value);
                    // Update existing record with DNA and fingerprint info
                    if (_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record))
                    {
                        record.DNA = dnaValue ?? (scanner.TouchDNAs.Count > 0 ? string.Join(", ", scanner.TouchDNAs) : "N/A");
                        record.Fingerprint = fingerprintValue ?? (scanner.Fingerprints.Count > 0 ? string.Join(", ", scanner.Fingerprints) : "N/A");
                    }
                }

                // Add the criminal record with bounty 2500, all else null/default
                _stationRecords.AddRecordEntry<CriminalRecord>(key, new CriminalRecord
                {

                    Status = Content.Shared.Security.SecurityStatus.None,
                    Reason = "Scanned by forensic scanner",
                    InitiatorName = "Forensic Scanner",
                    History = new System.Collections.Generic.List<CrimeHistory>()
                }, null);

                // Add to scanned records so it appears on the console
                _criminalRecordsConsole.AddScannedRecord(key);
            }

            OpenUserInterface(args.Args.User, (uid, scanner));
        }

        /// <remarks>
        /// Hosts logic common between OnUtilityVerb and OnAfterInteract.
        /// </remarks>
        private void StartScan(EntityUid uid, ForensicScannerComponent component, EntityUid user, EntityUid target)
        {
            _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, user, component.ScanDelay, new ForensicScannerDoAfterEvent(), uid, target: target, used: uid)
            {
                BreakOnMove = true,
                NeedHand = true
            });
        }

        private void OnUtilityVerb(EntityUid uid, ForensicScannerComponent component, GetVerbsEvent<UtilityVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess || component.CancelToken != null)
                return;

            var verb = new UtilityVerb()
            {
                Act = () => StartScan(uid, component, args.User, args.Target),
                IconEntity = GetNetEntity(uid),
                Text = Loc.GetString("forensic-scanner-verb-text"),
                Message = Loc.GetString("forensic-scanner-verb-message")
            };

            args.Verbs.Add(verb);
        }

        private void OnAfterInteract(EntityUid uid, ForensicScannerComponent component, AfterInteractEvent args)
        {
            if (component.CancelToken != null || args.Target == null || !args.CanReach)
                return;

            StartScan(uid, component, args.User, args.Target.Value);
        }

        private void OnAfterInteractUsing(EntityUid uid, ForensicScannerComponent component, AfterInteractUsingEvent args)
        {
            if (args.Handled || !args.CanReach)
                return;

            if (!TryComp<ForensicPadComponent>(args.Used, out var pad))
                return;

            foreach (var fiber in component.Fibers)
            {
                if (fiber == pad.Sample)
                {
                    _audioSystem.PlayPvs(component.SoundMatch, uid);
                    _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-match-fiber"), uid, args.User);
                    return;
                }
            }

            foreach (var fingerprint in component.Fingerprints)
            {
                if (fingerprint == pad.Sample)
                {
                    _audioSystem.PlayPvs(component.SoundMatch, uid);
                    _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-match-fingerprint"), uid, args.User);
                    return;
                }
            }

            _audioSystem.PlayPvs(component.SoundNoMatch, uid);
            _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-match-none"), uid, args.User);
        }

        private void OnBeforeActivatableUIOpen(EntityUid uid, ForensicScannerComponent component, BeforeActivatableUIOpenEvent args)
        {
            UpdateUserInterface(uid, component);
        }

        private void OpenUserInterface(EntityUid user, Entity<ForensicScannerComponent> scanner)
        {
            UpdateUserInterface(scanner, scanner.Comp);

            _uiSystem.OpenUi(scanner.Owner, ForensicScannerUiKey.Key, user);
        }

        private void OnPrint(EntityUid uid, ForensicScannerComponent component, ForensicScannerPrintMessage args)
        {
            var user = args.Actor;

            if (_gameTiming.CurTime < component.PrintReadyAt)
            {
                // This shouldn't occur due to the UI guarding against it, but
                // if it does, tell the user why nothing happened.
                _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), uid, user);
                return;
            }

            // Spawn a piece of paper.
            var printed = Spawn(component.MachineOutput, Transform(uid).Coordinates);
            _handsSystem.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);

            if (!TryComp<PaperComponent>(printed, out var paperComp))
            {
                Log.Error("Printed paper did not have PaperComponent.");
                return;
            }

            _metaData.SetEntityName(printed, Loc.GetString("forensic-scanner-report-title", ("entity", component.LastScannedName)));

            var text = new StringBuilder();

            text.AppendLine(Loc.GetString("forensic-scanner-interface-fingerprints"));
            foreach (var fingerprint in component.Fingerprints)
            {
                text.AppendLine(fingerprint);
            }
            text.AppendLine();
            text.AppendLine(Loc.GetString("forensic-scanner-interface-fibers"));
            foreach (var fiber in component.Fibers)
            {
                text.AppendLine(fiber);
            }
            text.AppendLine();
            text.AppendLine(Loc.GetString("forensic-scanner-interface-dnas"));
            foreach (var dna in component.TouchDNAs)
            {
                text.AppendLine(dna);
            }
            foreach (var dna in component.SolutionDNAs)
            {
                Log.Debug(dna);
                if (component.TouchDNAs.Contains(dna))
                    continue;
                text.AppendLine(dna);
            }
            text.AppendLine();
            text.AppendLine(Loc.GetString("forensic-scanner-interface-residues"));
            foreach (var residue in component.Residues)
            {
                text.AppendLine(residue);
            }

            _paperSystem.SetContent((printed, paperComp), text.ToString());
            _audioSystem.PlayPvs(component.SoundPrint, uid,
                AudioParams.Default
                .WithVariation(0.25f)
                .WithVolume(3f)
                .WithRolloffFactor(2.8f)
                .WithMaxDistance(4.5f));

            component.PrintReadyAt = _gameTiming.CurTime + component.PrintCooldown;
        }

        private void OnClear(EntityUid uid, ForensicScannerComponent component, ForensicScannerClearMessage args)
        {
            component.Fingerprints = new();
            component.Fibers = new();
            component.TouchDNAs = new();
            component.SolutionDNAs = new();
            component.LastScannedName = string.Empty;

            UpdateUserInterface(uid, component);
        }
    }
}
