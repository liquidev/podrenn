using Godot;
using System;

public class Player : RigidBody
{
   private Spatial[] hoverProbes;

   private Vehicle vehicle;

   [Export]
   public float maxAcceleration = 35000.0f;
   [Export]
   public float maxBraking = -16000.0f;
   [Export]
   public float accelerationRate = 100.0f;
   [Export]
   public float maxVelocity = 40.0f; // m/s
   [Export]
   public float steering = 0.05f;
   [Export]
   public float steeringAcceleration = 0.002f;
   [Export(PropertyHint.Range, "0,16")]
   public float hoverHeight = 1.0f;


   private float height;
   private Vector3 gravity;
   private float thrust;
   private float gravityField;
   private float frictionCoeff;

   private Vector3 floorNormal;

   private float acceleration = 0.0f;

   private float angle = 0.0f;
   private float steeringSpeed = 0.0f;

   public bool isBraking { get; private set; }

   private bool isNotSteering = false;
   private bool isSteeringLeft = false;
   private bool isSteeringRight = false;

   public override void _Ready()
   {
      hoverProbes = new Spatial[] {
         (Spatial)GetNode("Probes/FrontRight"),
         (Spatial)GetNode("Probes/FrontLeft"),
         (Spatial)GetNode("Probes/BackRight"),
         (Spatial)GetNode("Probes/BackLeft"),
      };
      vehicle = (Vehicle)GetNode("Vehicle");
      vehicle.TailLightsEnabled = false;
   }

   private Vector3 GetLocalDown()
   {
      return -Transform.basis.y;
   }

   private ProbeResult? ProbeIntersection(Spatial probe, Vector3 direction, float maxDistance)
   {
      var origin = ToGlobal(probe.Translation);
      var dict = GetWorld().DirectSpaceState.IntersectRay(origin, origin + direction * maxDistance, new Godot.Collections.Array { this });
      if (dict.Count > 0)
      {
         return new ProbeResult(probe, (Vector3)dict["position"], (Vector3)dict["normal"]);
      }
      else
      {
         return null;
      }
   }

   private ProbeResult? ProbeClosestIntersection(float maxDistance)
   {
      ProbeResult? highestIntersection = null;
      var maxDistanceFromProbe = float.PositiveInfinity;

      var i = 1;
      foreach (var probe in hoverProbes)
      {
         if (ProbeIntersection(probe, GetLocalDown(), maxDistance) is ProbeResult intersection)
         {
            var distanceFromProbe = ToGlobal(probe.Translation).DistanceTo(intersection.hitPosition);
            if (distanceFromProbe < maxDistanceFromProbe)
            {
               highestIntersection = intersection;
               maxDistanceFromProbe = distanceFromProbe;
            }
         }
         ++i;
      }

      return highestIntersection;
   }

   private void Hover()
   {
      if (ProbeClosestIntersection(100f) is ProbeResult intersection)
      {
         var ray = intersection.hitPosition - ToGlobal(intersection.probe.Translation);
         height = ray.Length();
      }
      else
      {
         height = 1000000.0f;
      }

      gravityField = Mathf.Clamp(height / (hoverHeight * 2.0f) - hoverHeight, 0.0f, 1.0f);
      GravityScale = gravityField;

      var yvelDamping = Math.Max(0.0f, hoverHeight - height - LinearVelocity.y * 0.2f) * Mass;
      ApplyCentralImpulse(Transform.basis.y * yvelDamping);

      thrust = Mathf.Pow(Math.Max(0.0f, hoverHeight - height), 3.0f) * 10.0f;
      ApplyCentralImpulse(Transform.basis.y * thrust * Mass);

      var frictionHeight = 6.0f;
      frictionCoeff = Math.Max(0.0f, (hoverHeight * frictionHeight - height) / (hoverHeight * frictionHeight));
      var frictionAmount = 10.0f;
      var frictionForce = -new Vector3(LinearVelocity.x, 0.0f, LinearVelocity.z) * frictionCoeff * frictionAmount;
      ApplyCentralImpulse(frictionForce);
   }

   private void KeepUpright()
   {
      var highestIntersection = ProbeClosestIntersection(5.0f);

      var targetNormal = highestIntersection is ProbeResult intersection ? intersection.hitNormal : new Vector3(0f, 1f, 0f);
      floorNormal = floorNormal.LinearInterpolate(targetNormal, 0.1f);

      var rotated = Basis.Identity.Rotated(Vector3.Up, angle);
      var up = floorNormal;
      var right = Transform.basis.z.Cross(up);
      var forward = right.Cross(up);

      var transform = Transform;
      transform.basis = new Basis(right, up, forward).Orthonormalized() * rotated;
      Transform = transform;
   }

   private void LimitVelocity()
   {
      var xzVelocityV = new Vector2(LinearVelocity.x, LinearVelocity.z);
      var xzVelocity = Math.Min(xzVelocityV.Length(), maxVelocity);
      xzVelocityV = xzVelocityV.Normalized() * xzVelocity;
      var linVel = LinearVelocity;
      linVel.x = xzVelocityV.x;
      linVel.z = xzVelocityV.y;
      LinearVelocity = linVel;
   }

   public override void _IntegrateForces(PhysicsDirectBodyState state)
   {
      LimitVelocity();
      gravity = state.TotalGravity;
      KeepUpright();
      Hover();
   }

   private void AccelerateAndBrake()
   {
      var forward = Transform.basis.z;
      forward.y = 0.0f;

      var wasBraking = isBraking;
      isBraking = false;

      var accelerating = false;
      var brakeDown = false;

      if (Input.IsActionPressed("accelerate"))
      {
         acceleration += accelerationRate;
         accelerating = true;
      }
      if (Input.IsActionPressed("brake"))
      {
         acceleration += -accelerationRate;
         accelerating = true;
         brakeDown = true;
      }
      if (!accelerating)
      {
         acceleration *= 0.975f;
      }
      acceleration = Mathf.Clamp(acceleration, maxBraking, maxAcceleration);
      if (frictionCoeff < 0.1f)
      {
         acceleration *= 0.9f;
      }

      AddCentralForce(forward * acceleration * Mathf.Clamp(frictionCoeff, 0.01f, 1.0f));

      var forwardDirection = Transform.basis.z.Dot(LinearVelocity);
      if (forwardDirection > 0 && brakeDown)
      {
         isBraking = true;
      }
      // Kind of jank but needed to not unnecessarily send the signal every frame.
      if (!wasBraking && isBraking)
      {
         vehicle.TailLightsEnabled = true;
      }
      if (wasBraking && !isBraking)
      {
         vehicle.TailLightsEnabled = false;
      }
   }

   private void Steer()
   {
      var isSteering = false;
      if (Input.IsActionPressed("steer_left"))
      {
         steeringSpeed += steeringAcceleration;
         isSteering = true;
      }
      if (Input.IsActionPressed("steer_right"))
      {
         steeringSpeed -= steeringAcceleration;
         isSteering = true;
      }
      if (!isSteering)
      {
         steeringSpeed *= 0.9f;
      }
      steeringSpeed = Mathf.Clamp(steeringSpeed, -steering, steering);
      angle += steeringSpeed * Mathf.Clamp(frictionCoeff / 0.75f, 0.0f, 1.0f);

      var turningSteeringSpeed = steering * 0.75f;

      var wasSteeringLeft = isSteeringLeft;
      isSteeringLeft = steeringSpeed > turningSteeringSpeed;
      if (!wasSteeringLeft && isSteeringLeft)
      {
         vehicle.TurnSignal = Vehicle.TurnSignalMode.Left;
      }

      var wasSteeringRight = isSteeringRight;
      isSteeringRight = steeringSpeed < -turningSteeringSpeed;
      if (!wasSteeringRight && isSteeringRight)
      {
         vehicle.TurnSignal = Vehicle.TurnSignalMode.Right;
      }

      var wasNotSteering = isNotSteering;
      isNotSteering = steeringSpeed >= -turningSteeringSpeed && steeringSpeed <= turningSteeringSpeed;
      if (!wasNotSteering && isNotSteering)
      {
         vehicle.TurnSignal = Vehicle.TurnSignalMode.Off;
      }
   }

   public override void _PhysicsProcess(float delta)
   {
      AccelerateAndBrake();
      Steer();
   }

   private struct ProbeResult
   {
      public Spatial probe;
      public Vector3 hitPosition;
      public Vector3 hitNormal;

      public ProbeResult(Spatial probe, Vector3 hitPosition, Vector3 hitNormal)
      {
         this.probe = probe;
         this.hitPosition = hitPosition;
         this.hitNormal = hitNormal;
      }
   }
}
