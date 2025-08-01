﻿using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSynthSystem))]
public sealed partial class SynthComponent : Component
{
    [DataField]
    public ComponentRegistry? AddComponents;

    [DataField]
    public ComponentRegistry? RemoveComponents;

    /// <summary>
    /// The final stun duration (after endurance skill) is divided by this number.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? StunResistance = 2.5f;

    [DataField, AutoNetworkedField]
    public bool CanUseGuns = false;

    [DataField, AutoNetworkedField]
    public bool CanUseMeleeWeapons = true;

    /// <summary>
    /// The blood reagent to give the synth.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> NewBloodReagent = "RMCSynthBlood";

    /// <summary>
    /// The damage modifier set to give the synth.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<DamageModifierSetPrototype> NewDamageModifier = "RMCSynth";

    [DataField, AutoNetworkedField]
    public LocId SpeciesName = "rmc-species-name-synth";

    /// <summary>
    /// I.E. 1st generation, 3rd generation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId Generation = "rmc-species-synth-generation-third";

    [DataField, AutoNetworkedField]
    public LocId FixedIdentityReplacement = "cm-chatsan-replacement-synth";

    [DataField, AutoNetworkedField]
    public Dictionary<RMCHealthIconTypes, ProtoId<HealthIconPrototype>> HealthIconOverrides = new()
    {
        [RMCHealthIconTypes.Healthy] = "RMCHealthIconHealthySynth",
        [RMCHealthIconTypes.DeadDefib] = "RMCHealthIconDeadSynth",
        [RMCHealthIconTypes.DeadClose] = "RMCHealthIconDeadSynth",
        [RMCHealthIconTypes.DeadAlmost] = "RMCHealthIconDeadSynth",
        [RMCHealthIconTypes.DeadDNR] = "RMCHealthIconDeadDNRSynth",
        [RMCHealthIconTypes.Dead] = "RMCHealthIconDeadSynth",
        [RMCHealthIconTypes.HCDead] = "RMCHealthIconDeadSynth",
    };

    /// <summary>
    /// If false, do not spawn a brain organ for this synth.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ChangeBrain = true;

    /// <summary>
    /// New brain organ to add when the synth is created.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId<OrganComponent> NewBrain = "RMCOrganSynthBrain";

    /// <summary>
    /// The time it takes to repair the synth.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(0);

    /// <summary>
    /// The time it takes to repair the synth, if you are the synth.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SelfRepairTime = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public FixedPoint2 CritThreshold = FixedPoint2.New(199);

    /// <summary>
    /// The tool quality needed to repair the synth brute damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Welding";

    [DataField]
    public DamageSpecifier? WelderDamageToRepair;

    [DataField]
    public DamageSpecifier? CableCoilDamageToRepair;

    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> WelderDamageGroup = "Brute";

    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> CableCoilDamageGroup = "Burn";

    [DataField, AutoNetworkedField]
    public string DamageVisualsColor = "#EEEEEE";


    [DataField, AutoNetworkedField]
    public bool HideGeneration = false;

    /// <summary>
    /// If true, use human health icons instead of synth icons.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UseHumanHealthIcons = false;


}
