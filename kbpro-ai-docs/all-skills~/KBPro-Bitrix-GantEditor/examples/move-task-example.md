# Worked example: move «[Dev][туторы] Робот пылесос - Система уровней» from Иван to Семён

User request: «передай туторы пылесоса Семёну, поставь после сборки робота».

1. `node scripts/read-plan.cjs --group 94` → plan.json
   (Иван resp=22, task id=30414 at index 4; Семён resp=128, «Сборка робота» block ends
   with its marker at index 6.)
2. Confirm with the user: found task 30414, destination = chain of resp=128 after index 6.
3. Copy plan.json → plan-new.json; cut the 30414 object from people[resp=22].tasks[4],
   insert into people[resp=128].tasks at index 7.
4. `node scripts/reflow.cjs --plan plan-new.json --orig plan.json`
   Expected console: Иван's finish moves 1 workday earlier, Семён's 1 later; roughly
   (tasks after index 4 of Иван) + (tasks after index 6 of Семён) + 1 in changes-dates;
   3 entries in changes-links (30414 itself, its old successor, its new successor).
5. Preview to the user: «Иван: 15.09 → 14.09, Семён: 15.09 → 16.09, сдвинется N задач.
   Применяю?» — wait for yes.
6. `node ../KBPro-Bitrix-GantCreator/scripts/apply-dates.cjs --updates changes-dates.json`
7. `node scripts/apply-links.cjs --links changes-links.json`
8. `node ../KBPro-Bitrix-GantCreator/scripts/verify.cjs --updates updates.json`
9. «Обновите гант (Ctrl+F5)».

Sanity numbers for this project: 142 planned tasks total, 5 chains; a mid-chain move
never changes tasks BEFORE the mutation points, so if changes-dates.json contains a
task earlier than both touched positions — stop and investigate before applying.
