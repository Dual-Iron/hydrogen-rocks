using BepInEx;
using RWCustom;
using System.Security.Permissions;
using UnityEngine;

#pragma warning disable CS0618 // Do not remove the following line.
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace HydrogenRocks;

[BepInPlugin("com.dual.hydrogen-rocks", "Hydrogen Rocks", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // This method runs when the plugin is enabled.
    public void OnEnable()
    {
        On.MultiplayerUnlocks.SandboxItemUnlocked += MultiplayerUnlocks_SandboxItemUnlocked;
        // We want rocks to explode after they stop flying.
        // To do this, we hijack `Weapon.ChangeMode` here:
        On.Weapon.ChangeMode += OnChangeMode;

        // Change when scavengers decide to pick up and use bomb-rocks.
        On.ScavengerAI.WeaponScore += ScavengerAI_WeaponScore;
    }

    private bool MultiplayerUnlocks_SandboxItemUnlocked(On.MultiplayerUnlocks.orig_SandboxItemUnlocked orig, MultiplayerUnlocks self, MultiplayerUnlocks.SandboxUnlockID unlockID)
    {
        return orig(self, unlockID) || unlockID == MultiplayerUnlocks.SandboxUnlockID.Scavenger;
    }

    void OnChangeMode(On.Weapon.orig_ChangeMode orig, Weapon self, Weapon.Mode newMode)
    {
        // If this is a rock that was just thrown, then explode the rock.
        if (self is Rock rock && rock.mode == Weapon.Mode.Thrown) {
            Explode(rock);
        }
        // Otherwise, run the original method instead.
        else {
            orig(self, newMode);
        }
    }

    void Explode(Rock rock)
    {
        // `pos` is short for position.
        // This makes the explosion happen somewhere between the rock's current position and its position in the previous update.
        Vector2 pos = Vector2.Lerp(rock.firstChunk.pos, rock.firstChunk.lastPos, 0.35f);

        // `rad` is short for radius and `alpha` describes how opaque a light should be.
        // This creates visual effects for the explosion.
        Color softRed = new(r: 1.0f, g: 0.4f, b: 0.3f);
        rock.room.AddObject(new SootMark(rock.room, pos, rad: 80f, bigSprite: true));
        rock.room.AddObject(new Explosion.ExplosionLight(pos, rad: 280.0f, alpha: 1.0f, lifeTime: 7, lightColor: softRed));
        rock.room.AddObject(new Explosion.ExplosionLight(pos, rad: 230.0f, alpha: 1.0f, lifeTime: 3, lightColor: Color.white));
        rock.room.AddObject(new ExplosionSpikes(rock.room, pos, _spikes: 14, innerRad: 30.0f, lifeTime: 9.0f, width: 7.0f, length: 170.0f, color: softRed));
        rock.room.AddObject(new ShockWave(pos, size: 330.0f, intensity: 0.045f, lifeTime: 5));

        // Create the actual explosion. This will damage creatures and throw nearby objects.
        rock.room.AddObject(new Explosion(
            room: rock.room,
            sourceObject: rock,
            pos: pos,
            lifeTime: 7,
            rad: 250f,
            force: 6.2f,
            damage: 2f,
            stun: 280f,
            deafen: 0.25f,
            killTagHolder: rock.thrownBy,
            killTagHolderDmgFactor: 0.7f,
            minStun: 160f,
            backgroundNoise: 1f
        ));

        // Play the grenade explosion sound effect.
        rock.room.PlaySound(SoundID.Bomb_Explode, pos);
        rock.room.InGameNoise(new Noise.InGameNoise(pos, strength: 9000.0f, sourceObject: rock, interesting: 1.0f));

        // Cause the screen to shake.
        rock.room.ScreenMovement(pos, bump: Vector2.zero, shake: 1.3f);

        // Destroy the rock.
        rock.abstractPhysicalObject.Destroy();
        rock.Destroy();
    }

    // This code is largely copied from ScavengerAI.WeaponScore in dnSpy. Don't be afraid to look at Rain World's code when writing your own!
    int ScavengerAI_WeaponScore(On.ScavengerAI.orig_WeaponScore orig, ScavengerAI self, PhysicalObject obj, bool pickupDropInsteadOfWeaponSelection)
    {
        // If the object being examined isn't a rock, return the vanilla value.
        if (obj is not Rock) {
            return orig(self, obj, pickupDropInsteadOfWeaponSelection);
        }

        // If the scav is only picking up the bomb, it doesn't hesitate.
        if (pickupDropInsteadOfWeaponSelection) {
            return 3;
        }
        // If the scav is actually using the bomb, but not trying to kill something, it doesn't use the bomb.
        if (self.currentViolenceType != ScavengerAI.ViolenceType.Lethal) {
            return 0;
        }
        // If the scav has a target, it judges if the bomb is viable to use before doing so.
        if (self.focusCreature != null) {
            if (ShouldAttack(self, self.focusCreature)) {
                return 3;
            }
            return 0;
        }
        // Otherwise, the scav refuses to throw a bomb, unless it's really scared.
        if (self.scared < 0.9f) {
            return 0;
        }
        return 1;
    }

    bool ShouldAttack(ScavengerAI ai, Tracker.CreatureRepresentation target)
    {
        // If the scav is too close to the target, don't throw the bomb.
        if (Vector2.Distance(ai.scavenger.mainBodyChunk.pos, ai.scavenger.room.MiddleOfTile(target.BestGuessForPosition())) < 300f) {
            return false;
        }
        // Examine each creature the scav knows about.
        foreach (var crit in ai.tracker.creatures) {
            // If it's pack members one, and the pack member is close to the target, then don't throw the bomb.
            if (crit.dynamicRelationship.currentRelationship.type == CreatureTemplate.Relationship.Type.Pack && Custom.ManhattanDistance(crit.BestGuessForPosition(), target.BestGuessForPosition()) < 7) {
                return false;
            }
        }
        // Ok throw the bomb
        return true;
    }
}
