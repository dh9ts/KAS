﻿// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.LogUtils;
using System;
using System.Linq;

namespace KASImpl {

class LinkUtilsImpl : ILinkUtils {
  /// <inheritdoc/>
  public ILinkTarget FindLinkTargetFromSource(ILinkSource source) {
    if (source.linkTargetPartId > 0) {
      var targetPart = FlightGlobals.FindPartByID(source.linkTargetPartId);
      if (targetPart != null) {
        return targetPart.FindModulesImplementing<ILinkTarget>().FirstOrDefault(
            t => t.isLinked && t.linkSourcePartId == source.part.flightID);
      }
    }
    return null;
  }

  /// <inheritdoc/>
  public ILinkSource FindLinkSourceFromTarget(ILinkTarget target) {
    if (target.linkSourcePartId > 0) {
      var sourcePart = FlightGlobals.FindPartByID(target.linkSourcePartId);
      if (sourcePart != null) {
        return sourcePart.FindModulesImplementing<ILinkSource>().FirstOrDefault(
            s => s.isLinked && s.linkTargetPartId == target.part.flightID);
      }
    }
    return null;
  }

  /// <inheritdoc/>
  public Part CoupleParts(AttachNode sourceNode, AttachNode targetNode,
                          bool toDominantVessel = false) {
    if (toDominantVessel) {
      var dominantVessel =
          Vessel.GetDominantVessel(sourceNode.owner.vessel, targetNode.owner.vessel);
      if (dominantVessel != targetNode.owner.vessel) {
        var tmp = sourceNode;
        sourceNode = targetNode;
        targetNode = tmp;
      }
    }
    var srcPart = sourceNode.owner;
    var srcVessel = srcPart.vessel;
    KASAPI.AttachNodesUtils.AddNode(srcPart, sourceNode);
    var tgtPart = targetNode.owner;
    var tgtVessel = tgtPart.vessel;
    KASAPI.AttachNodesUtils.AddNode(tgtPart, targetNode);
    DebugEx.Fine("Couple {0} to {1}",
                 KASAPI.AttachNodesUtils.DumpAttachNode(sourceNode),
                 KASAPI.AttachNodesUtils.DumpAttachNode(targetNode));

    sourceNode.attachedPart = tgtPart;
    sourceNode.attachedPartId = tgtPart.flightID;
    targetNode.attachedPart = srcPart;
    targetNode.attachedPartId = srcPart.flightID;
    tgtPart.attachMode = AttachModes.STACK;
    srcPart.Couple(tgtPart);
    // Depending on how active vessel has updated do either force active or make active. Note, that
    // active vessel can be EVA kerbal, in which case nothing needs to be adjusted.    
    // FYI: This logic was taken from ModuleDockingNode.DockToVessel.
    if (srcVessel == FlightGlobals.ActiveVessel) {
      FlightGlobals.ForceSetActiveVessel(sourceNode.owner.vessel);  // Use actual vessel.
      FlightInputHandler.SetNeutralControls();
    } else if (sourceNode.owner.vessel == FlightGlobals.ActiveVessel) {
      sourceNode.owner.vessel.MakeActive();
      FlightInputHandler.SetNeutralControls();
    }

    return srcPart;
  }

  /// <inheritdoc/>
  public Part DecoupleParts(Part part1, Part part2,
                            DockedVesselInfo vesselInfo1 = null,
                            DockedVesselInfo vesselInfo2 = null) {
    Part partToDecouple;
    DockedVesselInfo vesselInfo;
    if (part1.parent == part2) {
      DebugEx.Fine("Decouple {0} from {1}", part1, part2);
      partToDecouple = part1;
      vesselInfo = vesselInfo1;
    } else if (part2.parent == part1) {
      DebugEx.Fine("Decouple {0} from {1}", part2, part1);
      partToDecouple = part2;
      vesselInfo = vesselInfo2;
    } else {
      DebugEx.Warning("Cannot decouple {0} <=> {1} - not coupled!", part1, part2);
      return null;
    }

    if (vesselInfo != null) {
      // Simulate the IActivateOnDecouple behaviour since Undock() doesn't do it.
      var srcAttachNode = partToDecouple.FindAttachNodeByPart(partToDecouple.parent);
      if (srcAttachNode != null) {
        srcAttachNode.attachedPart = null;
        partToDecouple.FindModulesImplementing<IActivateOnDecouple>()
            .ForEach(m => m.DecoupleAction(srcAttachNode.id, true));
      }
      if (partToDecouple.parent != null) {
        var tgtAttachNode = partToDecouple.parent.FindAttachNodeByPart(partToDecouple);
        if (tgtAttachNode != null) {
          tgtAttachNode.attachedPart = null;
          partToDecouple.parent.FindModulesImplementing<IActivateOnDecouple>()
              .ForEach(m => m.DecoupleAction(tgtAttachNode.id, false));
        }
      }
      // Decouple and restore the name and hierarchy on the decoupled assembly.
      var vesselInfoCfg = new ConfigNode();
      vesselInfo.Save(vesselInfoCfg);
      DebugEx.Fine("Restore vessel info:\n{0}", vesselInfoCfg);
      partToDecouple.Undock(vesselInfo);
    } else {
      // Do simple decouple event which will screw the decoupled vessel root part.
      DebugEx.Warning("No vessel info found! Just decoupling");
      partToDecouple.decouple();
    }
    part1.vessel.CycleAllAutoStrut();
    part2.vessel.CycleAllAutoStrut();
    return partToDecouple;
  }
}

}  // namespace
