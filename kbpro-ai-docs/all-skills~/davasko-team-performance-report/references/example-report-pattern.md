# Example Report Pattern

Use this as the style target for the HTML report. The examples are shortened
from the 2026-07-16 report.

## Ranking Summary Cards

Good management cards are short and decisive:

```html
<div class="card">
  <b>Best balanced developer: Klyuenkov</b>
  <p>Good volume, almost no external fixes, low share of work outside MR. Main
  weakness: not every MR has an approval.</p>
</div>
```

Avoid internal process narration:

Do not explain internal formula edits in the visible report. Describe the
current evidence only.

## Concrete Employee Examples

Each employee tab must have examples before the metric table:

```html
<div class="examples">
  <h3>Concrete examples from changes</h3>
  <div class="cards">
    <div class="card">
      <b>What is good for the product</b>
      <p>Prefab-heavy and integration work: MouthCleaning water/floss stages,
      construction voice/sound, railway sound, playground final animations.
      For a producer this means real player-facing behavior: tools move, sound,
      react, and characters speak.</p>
    </div>
    <div class="card">
      <b>What lowered the score</b>
      <p>Repeated sound/fix/rework cycles in the same period, Debug.Log left in
      PlaygroundGenerateGameSystem, and private-to-protected widening in
      AirGunDirtElement. This is useful delivery with stabilization debt.</p>
    </div>
  </div>
</div>
```

## Metric Row Examples

The last column explains this employee, not the metric in general.

Good:

```html
<tr>
  <td>Rule compliance</td>
  <td>quality</td>
  <td class="num">5.5</td>
  <td><span class="pill risk">risk point</span></td>
  <td>Concrete issues: Debug.Log left in PlaygroundGenerateGameSystem;
  AirGunDirtElement fields/properties widened from private to protected to
  support BlotDirtElement.</td>
</tr>
```

Good:

```html
<tr>
  <td>Architecture discipline</td>
  <td>quality</td>
  <td class="num">7.5</td>
  <td><span class="pill good">strong side</span></td>
  <td>New MouthCleaning components use GameComponent/LogicSystem, sound uses
  ISoundAccessor, and UI state uses EventBus/EventBinding.</td>
</tr>
```

Bad:

```html
<tr>
  <td>Architecture discipline</td>
  <td>quality</td>
  <td class="num">7.5</td>
  <td>strong</td>
  <td>Shows how well changes follow architecture.</td>
</tr>
```

## CTO / AI Infrastructure Section

When a person works mostly on AI rules, skills, harnesses, knowledge base, or
developer automation, do not force them into the developer ranking. Use a
separate tab:

```html
<h2>Davletbaev Alexander <span>separate CTO/AI-infrastructure assessment</span></h2>
<p>Data confirms work on AI rules, skill descriptions, harness/URDT, judge
rules, token policy, documentation, and submodule synchronization. The right
acceptance criteria are pipeline operability, rule reproducibility, and reduced
manual work, not ordinary feature throughput.</p>
```

## Bitrix24 Join Example

When Bitrix data exists, add task evidence to the same employee tab and to the
weighted score:

```html
<tr>
  <td>Weighted tasks closed</td>
  <td>Bitrix value</td>
  <td class="num">34 h/week</td>
  <td><span class="pill good">strong side</span></td>
  <td>Closed high-complexity tasks in Railway and Dentistry while Git volume
  was moderate. This explains why code volume alone understates delivery.</td>
</tr>
```

```html
<tr>
  <td>Returned tasks</td>
  <td>Bitrix quality</td>
  <td class="num">18%</td>
  <td><span class="pill risk">risk point</span></td>
  <td>Several completed tasks returned from testing in the same product area,
  and the linked commits show later fixes. For management this means delivery
  happened, but producer/tester load increased after handoff.</td>
</tr>
```
