using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamOverlay.Backend.Mock;
using TeamOverlay.Core;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class ProfiledMockTeamBackendTests
    {
        [Test]
        public async Task ProfileName_ReplacesOnlyLocalDisplayName()
        {
            using (var backend = new ProfiledMockTeamBackend("김햄초"))
            {
                var states = await backend.GetTeamStateAsync(CancellationToken.None);

                Assert.That(states, Has.Count.EqualTo(4));
                Assert.That(
                    states.Single(state => state.MemberId == backend.LocalMemberId).DisplayName,
                    Is.EqualTo("김햄초"));
                Assert.That(
                    states.Where(state => state.MemberId != backend.LocalMemberId)
                        .Select(state => state.DisplayName),
                    Is.EqualTo(new[] { "뱁버드", "잔다", "메이비" }));
            }
        }

        [Test]
        public async Task LocalMutationEvent_ContainsProfiledSnapshot()
        {
            using (var backend = new ProfiledMockTeamBackend("테스터"))
            {
                var observer = new RecordingObserver<TeamEvent>();
                using (backend.Events.Subscribe(observer))
                {
                    await backend.CheckInAsync(CancellationToken.None);

                    Assert.That(observer.Values, Has.Count.EqualTo(1));
                    Assert.That(observer.Values[0].ActorMemberId, Is.EqualTo(backend.LocalMemberId));
                    Assert.That(observer.Values[0].State.DisplayName, Is.EqualTo("테스터"));
                    Assert.That(observer.Values[0].State.IsClockedIn, Is.True);
                }
            }
        }

        private sealed class RecordingObserver<T> : IObserver<T>
        {
            public List<T> Values { get; } = new List<T>();

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
                Assert.Fail(error.ToString());
            }

            public void OnNext(T value)
            {
                Values.Add(value);
            }
        }
    }
}
