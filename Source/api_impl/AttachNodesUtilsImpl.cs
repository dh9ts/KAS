﻿// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using System;
using UnityEngine;

namespace KASImpl {

/// <summary>Implements KASAPIv1.IAttachNodesUtils.</summary>
class AttachNodesUtilsImpl : KASAPIv1.IAttachNodesUtils {
  /// <inheritdoc/>
  public AttachNode CreateNode(Part part, string nodeName, Transform nodeTransform) {
    // Attach node wants the local coordinates! May be due to the prefab setup.
    var localNodeTransform = new GameObject(nodeName + "-autonode").transform;
    localNodeTransform.parent = part.transform;
    localNodeTransform.position = part.transform.InverseTransformPoint(nodeTransform.position);
    localNodeTransform.rotation = part.transform.rotation.Inverse() * nodeTransform.rotation;
    localNodeTransform.localScale = Vector3.one;  // The position has already the scale applied. 
    var attachNode = part.FindAttachNode(nodeName);
    if (attachNode != null) {
      DebugEx.Warning("Not creating attach node {0} for {1} - already exists", nodeName, part);
    } else {
      attachNode = new AttachNode(nodeName, localNodeTransform, 0, AttachNodeMethod.FIXED_JOINT,
                                  crossfeed: true, rigid: false);
    }
    attachNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
    attachNode.nodeType = AttachNode.NodeType.Stack;
    attachNode.nodeTransform = localNodeTransform;
    attachNode.owner = part;
    AddNode(part, attachNode);
    return attachNode;
  }

  /// <inheritdoc/>
  public void AddNode(Part part, AttachNode attachNode) {
    if (attachNode.owner != part) {
      DebugEx.Warning(
          "Former owner of the attach node doesn't match the new one: old={0}, new={1}",
          attachNode.owner, part);
      attachNode.owner = part;
    }
    if (part.attachNodes.IndexOf(attachNode) == -1) {
      DebugEx.Fine("Adding node {0} to {1}", NodeId(attachNode), part);
      part.attachNodes.Add(attachNode);
    }
  }

  /// <inheritdoc/>
  public void DropNode(Part part, AttachNode attachNode) {
    if (attachNode.attachedPart != null) {
      DebugEx.Error("Not dropping an attached node: {0}", NodeId(attachNode));
      return;
    }
    if (part.attachNodes.IndexOf(attachNode) != -1) {
      DebugEx.Fine("Drop attach node: {0}", NodeId(attachNode));
      part.attachNodes.Remove(attachNode);
      attachNode.attachedPartId = 0;  // Just in case.
    }
  }

  /// <inheritdoc/>
  public string NodeId(AttachNode an) {
    return an == null
        ? "[AttachNode:NULL]"
        : string.Format(
            "[AttachNode:id={0},host={1},to={2}]",
            an.id, DebugEx.ObjectToString(an.owner), DebugEx.ObjectToString(an.attachedPart));
  }

  /// <inheritdoc/>
  public AttachNode ParseNodeFromString(Part ownerPart, string def, string nodeId) {
    // The logic is borrowed from PartLoader.ParsePart.
    var array = def.Split(',');
    try {
      if (array.Length < 6) {
        throw new ArgumentException(string.Format("Not enough components: {0}", array.Length));
      }
      var attachNode = new AttachNode();
      attachNode.owner = ownerPart;
      attachNode.id = nodeId;
      var factor = ownerPart.rescaleFactor;
      attachNode.position = new Vector3(
          float.Parse(array[0]), float.Parse(array[1]), float.Parse(array[2])) * factor;
      attachNode.orientation = new Vector3(
          float.Parse(array[3]), float.Parse(array[4]), float.Parse(array[5])) * factor;
      attachNode.originalPosition = attachNode.position;
      attachNode.originalOrientation = attachNode.orientation;
      attachNode.size = array.Length >= 7 ? int.Parse(array[6]) : 1;
      attachNode.attachMethod = array.Length >= 8
          ? (AttachNodeMethod)int.Parse(array[7])
          : AttachNodeMethod.FIXED_JOINT;
      if (array.Length >= 9) {
        attachNode.ResourceXFeed = int.Parse(array[8]) > 0;
      }
      if (array.Length >= 10) {
        attachNode.rigid = int.Parse(array[9]) > 0;
      }
      attachNode.nodeType = AttachNode.NodeType.Stack;
      return attachNode;
    }
    catch (Exception ex) {
      DebugEx.Error("Cannot parse node {0} for part {1} from: {2}\nError: {3}",
                    nodeId, ownerPart, def, ex.Message);
      return null;
    }
  }

  /// <inheritdoc/>
  public Transform GetTransformForNode(Part ownerPart, AttachNode an) {
    if (an.owner != ownerPart) {
      DebugEx.Warning("Attach node {0} doesn't belong to part {1}", NodeId(an), ownerPart);
    }
    var objectName = "attachNode-" + an.id;
    var nodeTransform = Hierarchy.FindPartModelByPath(ownerPart, objectName);
    if (nodeTransform != null) {
      return nodeTransform;
    }
    nodeTransform = new GameObject(objectName).transform;
    Hierarchy.MoveToParent(
        nodeTransform,
        Hierarchy.GetPartModelTransform(ownerPart),
        newPosition: an.position / ownerPart.rescaleFactor,
        newRotation: Quaternion.LookRotation(an.orientation));
    return nodeTransform;
  }
}

}  // namespace
