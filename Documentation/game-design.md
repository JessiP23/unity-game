# Game design

Mannequins move while unobserved. Observation transitions green → orange grace → red. Actual movement above a configurable jitter threshold raises integer suspicion. Discovery captures the player into the warehouse without destroying identity. A free teammate can rescue them. Required objectives plus a configured number of escapes win the night. Dawn, or every mannequin captured with no rescue path left, loses it.

The local prototype starts the night immediately with two mannequins and one AI guard. Tab is only a local way to control the other mannequin. Online play is a later transport in front of the same authority.

Default discovery still requires one more valid movement after the suspicion threshold. `continuedMovementToDiscover` can require discovery on the threshold event instead.
