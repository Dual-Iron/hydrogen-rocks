using BepInEx;
using UnityEngine;

namespace HydrogenRocks;

[BepInPlugin("com.dual.hydrogen-rocks", "Hydrogen Rocks", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // This method runs when the plugin is enabled.
    public void OnEnable()
    {
        // We want rocks to explode after they stop flying.
        // To do this, we hijack `Weapon.ChangeMode`:
        On.Weapon.ChangeMode += OnChangeMode;
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
}
