/**
 * ❌ BAD EXAMPLE 05: Raw Magic String Commands Anti-Pattern
 * 
 * WHY THIS IS WRONG:
 * 1. Using raw comma-separated strings like "click,drag,swipe,typo_here"
 *    has no compile-time checking in C# and no schema validation.
 * 2. Typos in strings ("dbclick" vs "double_click") cause silent testing failures.
 * 3. URDT requires the strongly typed enum `UrdtCommandType` and `List<UrdtCommandType>`.
 */

// ❌ ANTI-PATTERN: Serializing commands as an unvalidated raw string
public class BadTargetConfig : MonoBehaviour
{
    // ❌ WRONG: Raw string instead of strongly typed List<UrdtCommandType>
    [SerializeField] private string supportedCommands = "inspect,query,clik,dragg"; // Typos!
}
