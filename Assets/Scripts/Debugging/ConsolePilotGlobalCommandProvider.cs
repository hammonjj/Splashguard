using System.Collections.Generic;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Eventing.DebugEvents;
using ConsolePilot.Commands;
using ConsolePilot.Core;
using UnityEngine;

namespace BitBox.Toymageddon.Debugging
{
    [DisallowMultipleComponent]
    public sealed class ConsolePilotGlobalCommandProvider : MonoBehaviour, IConsoleCommandProvider
    {
        public void RegisterCommands(IConsoleCommandRegistry registry)
        {
            registry.Register(new KillAllEnemiesCommand(), out _);
        }
    }

    public sealed class KillAllEnemiesCommand : IConsoleCommand
    {
        public KillAllEnemiesCommand()
        {
            Descriptor = new CommandDescriptor(
                "killallenemies",
                "Kills all enemies currently present.",
                "killallenemies",
                new[] { "KillAllEnemies", "kill_all_enemies" });
        }

        public CommandDescriptor Descriptor { get; }

        public CommandResult Execute(CommandContext context, IReadOnlyList<string> arguments)
        {
            if (arguments.Count > 0)
            {
                return CommandResult.Info("Usage: killallenemies");
            }

            MessageBus globalMessageBus = GlobalStaticData.GlobalMessageBus;
            if (globalMessageBus == null)
            {
                return CommandResult.Fail("Global message bus is not available.");
            }

            int targetCount = globalMessageBus.GetSubscriberCount<KillAllEnemiesEvent>();
            globalMessageBus.Publish(new KillAllEnemiesEvent());

            return CommandResult.Ok(targetCount == 1
                ? "KillAllEnemies sent to 1 current enemy."
                : $"KillAllEnemies sent to {targetCount} current enemies.");
        }
    }
}
