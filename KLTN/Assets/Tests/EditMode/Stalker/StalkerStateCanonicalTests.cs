using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EchoProtocol.AI.Stalker.Tests
{
    public sealed class StalkerStateCanonicalTests
    {
        private const string StalkerStateTypeName = "EchoProtocol.AI.Stalker.StalkerState";

        [Test]
        public void STK_R_001_StalkerState_ContainsExactlyCanonicalSixStates()
        {
            var stalkerStateType = FindTypeByFullName(StalkerStateTypeName);
            Assert.That(stalkerStateType, Is.Not.Null, $"Could not find production type '{StalkerStateTypeName}' in the loaded Unity AppDomain.");
            Assert.That(stalkerStateType.IsEnum, Is.True, $"Production type '{StalkerStateTypeName}' must be an enum.");

            var actualStateNames = Enum.GetNames(stalkerStateType);
            var canonicalStateNames = new HashSet<string>
            {
                "PATROL",
                "DETECT",
                "CHASE",
                "ATTACK",
                "RECOVER",
                "SEARCH"
            };

            Assert.That(actualStateNames, Has.Length.EqualTo(6), "Canonical Stalker FSM must contain exactly six states.");

            foreach (var canonicalStateName in canonicalStateNames)
            {
                Assert.That(actualStateNames, Does.Contain(canonicalStateName), $"Missing canonical Stalker FSM state: {canonicalStateName}.");
            }

            foreach (var actualStateName in actualStateNames)
            {
                Assert.That(canonicalStateNames.Contains(actualStateName), Is.True, $"Unexpected non-canonical Stalker FSM state: {actualStateName}.");
            }
        }

        private static Type FindTypeByFullName(string fullTypeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
