﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

/**
 * Sample component that shows how to use Obi Rope to create a grappling hook for a 2.5D game.
 * 95% of the code is the grappling hook logic (user input, scene raycasting, launching, attaching the hook, etc) and parameter setup,
 * to show how to use Obi completely at runtime. This might not be practical for real-world scenarios,
 * but illustrates how to do it.
 *
 * Note that the choice of using actual rope simulation for grapple dynamics is debatable. Usually
 * a simple spring works better both in terms of performance and controllability. 
 *
 * If complex interaction is required with the scene, a purely geometry-based approach (ala Worms ninja rope) can
 * be the right choice under certain circumstances.
 */
public class MagnetHook : MonoBehaviour
{

    public ObiCollider character;
    public float hookExtendRetractSpeed = 2;
    public Material material;

    private ObiRope rope;
    private ObiCatmullRomCurve curve;
    private ObiSolver solver;
    private ObiRopeCursor cursor;
    private AdjustRopeLength ropeAdjuster;

    private RaycastHit hookAttachment;
    private bool attached = false;

    private void Awake()
    {

        // Create both the rope and the solver:	
        rope = gameObject.AddComponent<ObiRope>();
        curve = gameObject.AddComponent<ObiCatmullRomCurve>();
        solver = gameObject.AddComponent<ObiSolver>();

        // Provide a solver and a curve:
        rope.Solver = solver;
        rope.ropePath = curve;
        rope.GetComponent<MeshRenderer>().material = material;

        // Configure rope and solver parameters:
        rope.resolution = 0.2f;
        rope.BendingConstraints.stiffness = 1f;
        rope.uvScale = new Vector2(1, 0.24f);
        rope.normalizeV = false;
        rope.uvAnchor = 0;
        rope.sectionThicknessScale = 2;

        solver.substeps = 1;
        solver.parameters.gravity = new Vector3(0, 0.5f, 0);
        solver.parameters.damping = 0.57f;
        solver.distanceConstraintParameters.iterations = 5;
        solver.pinConstraintParameters.iterations = 5;
        solver.bendingConstraintParameters.iterations = 1;
        solver.particleCollisionConstraintParameters.enabled = false;
        solver.volumeConstraintParameters.enabled = false;
        solver.densityConstraintParameters.enabled = false;
        solver.stitchConstraintParameters.enabled = false;
        solver.skinConstraintParameters.enabled = false;
        solver.tetherConstraintParameters.enabled = false;

        // Add a cursor to be able to change rope length:
        cursor = rope.gameObject.AddComponent<ObiRopeCursor>();
        cursor.normalizedCoord = 0;
        cursor.direction = true;

        ropeAdjuster = gameObject.AddComponent<AdjustRopeLength>();

    }

    /**
	 * Raycast against the scene to see if we can attach the hook to something.
	 */
    private void LaunchHook()
    {

        // Get the mouse position in the scene, in the same XY plane as this object:
        Vector3 mouse = Input.mousePosition;
        mouse.z = 1;// transform.position.z - Camera.main.transform.position.z;
        Vector3 mouseInScene = Camera.main.ScreenToWorldPoint(mouse);

        // Get a ray from the character to the mouse:
        Ray ray = new Ray(Camera.main.transform.position, mouseInScene - Camera.main.transform.position);

        // Raycast to see what we hit:
        if (Physics.Raycast(ray, out hookAttachment))
        {

            // We actually hit something, so attach the hook!
            StartCoroutine(AttachHook());
        }

    }

    private IEnumerator AttachHook()
    {
        Vector3 hitPoint = hookAttachment.point;
        Vector3 localHit = curve.transform.InverseTransformPoint(hookAttachment.transform.position);

        // Procedurally generate the initial rope shape (a simple straight line):
        curve.controlPoints.Clear();
        curve.controlPoints.Add(Vector3.zero);
        curve.controlPoints.Add(Vector3.zero);
        curve.controlPoints.Add(localHit);
        curve.controlPoints.Add(localHit);

        // Generate the particle representation of the rope (wait until it has finished):
        yield return rope.GeneratePhysicRepresentationForMesh();

        // Pin both ends of the rope (this enables two-way interaction between character and rope):
        ObiPinConstraintBatch pinConstraints = rope.PinConstraints.GetFirstBatch();
        pinConstraints.AddConstraint(0, character, transform.localPosition, 0);
        pinConstraints.AddConstraint(rope.UsedParticles - 1, hookAttachment.collider.GetComponent<ObiColliderBase>(),
                                                          hookAttachment.collider.transform.InverseTransformPoint(hookAttachment.point), 0);

        // Add the rope to the solver to begin the simulation:
        rope.AddToSolver(null);
        rope.GetComponent<MeshRenderer>().enabled = true;

        attached = true;

        if (ropeAdjuster)
        {
        }
    }

    private void DetachHook()
    {

        // Detach hook:
        rope.RemoveFromSolver(null);
        rope.GetComponent<MeshRenderer>().enabled = false;

        attached = false;

    }

    private void Update()
    {

        if (Input.GetMouseButtonDown(0))
        {
            if (!attached)
                LaunchHook();
            else
                DetachHook();
        }

        if (Input.GetKey(KeyCode.W))
        {
            cursor.ChangeLength(rope.RestLength - hookExtendRetractSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S))
        {
            cursor.ChangeLength(rope.RestLength + hookExtendRetractSpeed * Time.deltaTime);
        }
    }
}
