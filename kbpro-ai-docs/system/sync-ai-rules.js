import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { readSafe, writeSafe, copyTextSafe } from './lib/fs-utf8.js';
import { mergeManagedBlock } from './lib/managed-block-parser.js';
import { parseFrontmatter } from './lib/frontmatter.js';
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 1. Project Root and Submodule Root Auto-Detection
const submoduleRoot = path.resolve(__dirname, '..'); // system/ parent is the submodule root

let projectRoot = submoduleRoot;
function isUnityProjectRoot(candidate) {
  return fs.existsSync(path.join(candidate, 'Assets')) &&
    fs.existsSync(path.join(candidate, 'ProjectSettings', 'ProjectVersion.txt'));
}

while (true) {
  if (isUnityProjectRoot(projectRoot)) {
    break;
  }

  const parent = path.dirname(projectRoot);
  if (parent === projectRoot) {
    const unityRootCandidate = path.resolve(submoduleRoot, '../../..');
    projectRoot = isUnityProjectRoot(unityRootCandidate) ? unityRootCandidate : submoduleRoot;
    break;
  }

  projectRoot = parent;
}

const isDevRepo = path.resolve(submoduleRoot) === path.resolve(projectRoot)
  && fs.existsSync(path.join(submoduleRoot, 'skills'));

console.log('=== sync-ai-rules ===');
console.log(`Submodule Root: ${submoduleRoot}`);
console.log(`Project Root:   ${projectRoot}`);
if (isDevRepo) console.log('Mode:           DEV (framework repo) — local IDE skill copies skipped');
console.log('');

// 2. Locate Rules Directory
let rulesDir = '';
const rulesCandidates = [
  path.join(submoduleRoot, 'ide-rules'),   // шаблоны правил живут в системе управления (сабмодуль), НЕ в БЗ-контенте
  path.join(projectRoot, 'Assets', 'DavASko', 'davasko-ai-docs', 'ide-rules'),
  path.join(projectRoot, 'davasko-ai-docs', 'ide-rules'),
  path.join(submoduleRoot, 'llm-wiki', 'raw', 'ide-rules')     // legacy fallback: когда правила лежали в контенте llm-wiki
];
for (const c of rulesCandidates) {
  if (fs.existsSync(c)) {
    rulesDir = c;
    break;
  }
}

function shouldHaveBom(filePath) {
  if (path.basename(filePath) === 'SKILL.md') return false;
  return path.extname(filePath).toLowerCase() === '.md';
}

function writeText(filePath, content) {
  writeSafe(filePath, content, shouldHaveBom(filePath));
}

function readText(filePath) {
  return readSafe(filePath);
}

function copyTextFile(src, dest) {
  if (!fs.existsSync(src)) return;
  copyTextSafe(src, dest, shouldHaveBom(dest));
}

function mergeManagedRule(srcPath, dstPath) {
  const inner = readText(srcPath).trim();
  const existing = fs.existsSync(dstPath) ? readText(dstPath) : '';
  const result = mergeManagedBlock(existing, 'DavASkoLLMWiki', inner);
  writeText(dstPath, result);
  return existing === '' ? 'created' : 'merged';
}

function copyDirectory(srcDir, destDir) {
  if (!fs.existsSync(srcDir)) return;
  if (!fs.existsSync(destDir)) {
    fs.mkdirSync(destDir, { recursive: true });
  }
  const items = fs.readdirSync(srcDir);
  items.forEach(item => {
    if (item.endsWith('.meta')) return; 
    const srcPath = path.join(srcDir, item);
    const destPath = path.join(destDir, item);
    const stat = fs.statSync(srcPath);
    if (stat.isDirectory()) {
      copyDirectory(srcPath, destPath);
    } else {
      const ext = path.extname(item).toLowerCase();
      const textExtensions = ['.md', '.txt', '.json', '.ps1', '.js', '.mdc', '.yml', '.yaml', '.clinerules', '.cursorrules', '.windsurfrules'];
      if (textExtensions.includes(ext) || item === 'AGENTS.md' || item === 'CLAUDE.md' || item === 'GEMINI.md') {
        copyTextFile(srcPath, destPath);
      } else {
        fs.copyFileSync(srcPath, destPath);
      }
    }
  });
}

function bundleSkillSystemRefs(dest) {
  const destSkillMd = path.join(dest, 'SKILL.md');
  if (!fs.existsSync(destSkillMd)) return;
  let content = readText(destSkillMd);

  const systemDocsDir = path.join(submoduleRoot, 'system', 'docs');
  const systemScriptsDir = path.join(submoduleRoot, 'system', 'scripts');

  const collect = (re) => {
    const names = new Set();
    let m;
    while ((m = re.exec(content)) !== null) names.add(m[1]);
    return names;
  };

  collect(/\.\.\/\.\.\/system\/docs\/([A-Za-z0-9._-]+)/g).forEach(file => {
    const sub = (file === 'setup-new-wiki.md') ? 'examples' : 'references';
    copyTextFile(path.join(systemDocsDir, file), path.join(dest, sub, file));
  });
  collect(/\.\.\/\.\.\/system\/scripts\/([A-Za-z0-9._-]+)/g).forEach(file => {
    copyTextFile(path.join(systemScriptsDir, file), path.join(dest, 'scripts', file));
  });

  content = rewriteSystemRefs(content);
  writeText(destSkillMd, content);
}

function rewriteSystemRefs(content) {
  return content
    .replace(/\.\.\/\.\.\/system\/docs\/setup-new-wiki\.md/g, 'examples/setup-new-wiki.md')
    .replace(/\.\.\/\.\.\/system\/docs\//g, 'references/')
    .replace(/\.\.\/\.\.\/system\/scripts\//g, 'scripts/');
}

function deleteFolderRecursive(dirPath) {
  if (fs.existsSync(dirPath)) {
    fs.readdirSync(dirPath).forEach(file => {
      const curPath = path.join(dirPath, file);
      if (fs.lstatSync(curPath).isDirectory()) {
        deleteFolderRecursive(curPath);
      } else {
        fs.unlinkSync(curPath);
      }
    });
    fs.rmdirSync(dirPath);
  }
}

// 3. Synchronize main rule files
if (rulesDir) {
  console.log(`Source rules dir found: ${rulesDir}`);
  
  // Rule #1: AGENTS.md is the only source of truth.
  const srcAgents = path.join(rulesDir, 'AGENTS.md');
  const dstAgents = path.join(projectRoot, 'AGENTS.md');
  if (fs.existsSync(srcAgents)) {
    mergeManagedRule(srcAgents, dstAgents);
    console.log(`  [OK] Rule: AGENTS.md  ->  AGENTS.md`);
  }

  // Generate reference files for other environments
  const referenceContent = "Please follow the core agent rules defined in: [AGENTS.md](AGENTS.md)\n";
  const refTargets = [
    { dst: '.cursorrules' },
    { dst: 'GEMINI.md' },
    { dst: '.windsurfrules' },
    { dst: '.clinerules' },
    { dst: 'CLAUDE.md' }
  ];
  
  refTargets.forEach(t => {
    const dstPath = path.join(projectRoot, t.dst);
    writeText(dstPath, referenceContent);
    console.log(`  [OK] Link Generated: ${t.dst} -> AGENTS.md`);
  });
} else {
  console.log('Rules directory (ide-rules) not found. Skipping main rules synchronization.');
}

// 4. Synchronize Claude Commands
const claudeCmdsSource = path.join(submoduleRoot, 'llm-wiki', 'raw', 'claude-commands');
const claudeCmdsDest = path.join(projectRoot, '.claude', 'commands');
if (fs.existsSync(claudeCmdsSource)) {
  if (!fs.existsSync(claudeCmdsDest)) {
    fs.mkdirSync(claudeCmdsDest, { recursive: true });
  }
  fs.readdirSync(claudeCmdsSource).forEach(file => {
    if (file.endsWith('.md')) {
      copyTextFile(path.join(claudeCmdsSource, file), path.join(claudeCmdsDest, file));
      console.log(`  [OK] Claude command: ${file}  ->  .claude/commands/${file}`);
    }
  });
}

// 5. Gather skills from all-skills
const args = process.argv.slice(2);
const isGlobal = args.includes('--global') || args.includes('-g');

let allSkillsDir = path.join(submoduleRoot, 'all-skills~');
if (!fs.existsSync(allSkillsDir) && fs.existsSync(path.join(submoduleRoot, 'skills'))) {
    allSkillsDir = path.join(submoduleRoot, 'skills');
}
let activeSkillNames = null;
let allSkillData = [];



function parseSkillFrontmatter(content) {
    const { meta, hasFrontmatter } = parseFrontmatter(content);
    let name = meta.name || '';
    let description = meta.description || '';
    let isMcp = Boolean(meta.mcp);

    if (!name) {
        const nameMatch = content.match(/^name:\s*(.+)$/m);
        if (nameMatch) name = nameMatch[1].trim().replace(/^['"]|['"]$/g, '');
    }

    if (!description) {
        const descMatch = content.match(/^description:\s*(.+)$/m);
        if (descMatch) description = descMatch[1].trim().replace(/^['"]|['"]$/g, '');
    }

    if (!description) {
        const lines = content.split('\n');
        for (let line of lines) {
            line = line.trim();
            if (line && !line.startsWith('#') && !line.startsWith('---') && !line.startsWith('name:') && !line.startsWith('description:') && !line.startsWith('*') && !line.startsWith('>')) {
                description = line;
                break;
            }
        }
    }

    if (typeof description === 'string') {
        description = description.replace(/\r?\n/g, ' ').replace(/\s+/g, ' ').trim();
        // Escape pipes for markdown table
        description = description.replace(/\|/g, '\\|');
    }
    
    return { name, description, isMcp };
}

if (fs.existsSync(allSkillsDir)) {
    fs.readdirSync(allSkillsDir).forEach(file => {
        if (file.endsWith('.meta')) return;
        const skillPath = path.join(allSkillsDir, file);
        if (fs.statSync(skillPath).isDirectory() && fs.existsSync(path.join(skillPath, 'SKILL.md'))) {

            
            const skillContent = readText(path.join(skillPath, 'SKILL.md'));
            const metadata = parseSkillFrontmatter(skillContent) || { name: file, description: 'No description', isMcp: false };
            
            allSkillData.push({
                folderName: file,
                path: skillPath,
                name: metadata.name || file,
                description: metadata.description,
                isMcp: metadata.isMcp
            });
        }
    });
}

// (Removed Harness dispatcher forced logic per user request)

const hardSkills = allSkillData.filter(s => s.isMcp);
const softSkills = allSkillData.filter(s => !s.isMcp);

console.log(`\nFound ${hardSkills.length} MCP/Hard skills and ${softSkills.length} Soft skills.`);

// All individual skills remain strictly in one canonical place: all-skills~/
// The orchestrator 'davasko-skill-orchestrator' is the ONLY skill deployed into IDE folders.
const requiredIdeFolders = ['davasko-skill-orchestrator'];

// 5a. Clean up copied individual skill folders from root IDE folders
const skillDestFolders = ['.agents/skills', '.codex/skills', '.claude/skills', '.gemini/skills'];
skillDestFolders.forEach(folder => {
  const targetFolder = path.join(projectRoot, folder);
  if (fs.existsSync(targetFolder)) {
    fs.readdirSync(targetFolder).forEach(file => {
      const fullPath = path.join(targetFolder, file);
      try {
        if (fs.existsSync(fullPath) && fs.statSync(fullPath).isDirectory() && !requiredIdeFolders.includes(file)) {
          deleteFolderRecursive(fullPath);
          console.log(`  [CLEAN] Removed copied skill from IDE folder: ${folder}/${file}`);
        }
      } catch (err) {
        // Skip inaccessible or broken symlink
      }
    });
  }
});

if (isGlobal) {
  const homeDir = process.env.USERPROFILE || process.env.HOME || process.env.HOMEPATH;
  if (homeDir) {
    const globalSkillFolders = [
      path.join(homeDir, '.codex', 'skills'),
      path.join(homeDir, '.agents', 'skills'),
      path.join(homeDir, '.claude', 'skills'),
      path.join(homeDir, '.gemini', 'config', 'skills')
    ];
    globalSkillFolders.forEach(gf => {
      if (fs.existsSync(gf)) {
        fs.readdirSync(gf).forEach(file => {
          const fullPath = path.join(gf, file);
          try {
            if (fs.existsSync(fullPath) && fs.statSync(fullPath).isDirectory() && !requiredIdeFolders.includes(file)) {
              deleteFolderRecursive(fullPath);
              console.log(`  [CLEAN] Removed copied skill from global folder: ${file}`);
            }
          } catch (err) {
            // Skip inaccessible or broken symlink
          }
        });
      }
    });
  }
}

// 5b. Clean up copied individual rule files from IDEs (only davasko-skill-orchestrator is retained)
const singleRuleDestinations = [
  { dir: '.claude/commands', ext: '.md' },
  { dir: '.cursor/rules', ext: '.mdc' },
  { dir: '.windsurf/rules', ext: '.md' },
  { dir: '.cline/rules', ext: '.md' },
  { dir: '.roo/rules', ext: '.md' },
  { dir: '.github/instructions', ext: '.instructions.md' }
];

let claudeCmdNames = [];
if (fs.existsSync(claudeCmdsSource)) {
  claudeCmdNames = fs.readdirSync(claudeCmdsSource).filter(f => f.endsWith('.md')).map(f => path.parse(f).name);
}

singleRuleDestinations.forEach(fd => {
  const targetDir = path.join(projectRoot, fd.dir);
  if (fs.existsSync(targetDir)) {
    fs.readdirSync(targetDir).forEach(file => {
      const ext = path.extname(file).toLowerCase();
      let baseName = path.parse(file).name;
      if (fd.ext === '.instructions.md') {
        if (file.endsWith('.instructions.md')) {
          baseName = file.replace(/\.instructions\.md$/, '');
        } else {
          return;
        }
      }

      if (fd.dir === '.claude/commands' && claudeCmdNames.includes(baseName)) {
        return; // do not delete standard claude commands
      }

      if (ext === fd.ext || (fd.ext === '.instructions.md' && file.endsWith('.instructions.md'))) {
        if (!requiredIdeFolders.includes(baseName)) {
          fs.unlinkSync(path.join(targetDir, file));
          console.log(`  [CLEAN] Removed copied skill rule: ${fd.dir}/${file}`);
        }
      }
    });
  }
});

// 6. Generate Orchestrator (Single source of skills for all IDEs)
if (allSkillData.length > 0) {
  let orchestratorContent = `---
name: davasko-skill-orchestrator
description: Главный маршрутизатор (Orchestrator). Используй этот скил ВСЕГДА, когда пользователь просит выполнить специализированную задачу.
---
# Skill Orchestrator
CRITICAL INSTRUCTION: Ниже представлен каталог доступных проектных скилов. Если задача пользователя совпадает с триггером (Description), вы ОБЯЗАНЫ использовать инструмент \`view_file\`, чтобы прочитать относительный путь к \`SKILL.md\` выбранного скила ДО начала выполнения задачи. Указанные пути отсчитываются от корня проекта. Если ваш инструмент чтения файлов требует абсолютный путь, сформируйте его, добавив корень проекта (workspace root) к указанному относительному пути.

| Skill Name | Triggers (Description) | Relative Path |
|---|---|---|
`;

  allSkillData.forEach(s => {
    const relativePath = path.relative(projectRoot, s.path).replace(/\\/g, '/');
    const uriPath = `${relativePath}/SKILL.md`;
    orchestratorContent += `| ${s.name} | ${s.description} | ${uriPath} |\n`;
  });

  const orchestratorName = 'davasko-skill-orchestrator';
  const dirDestinations = isDevRepo ? [] : [
    path.join(projectRoot, '.agents', 'skills', orchestratorName),
    path.join(projectRoot, '.codex', 'skills', orchestratorName),
    path.join(projectRoot, '.claude', 'skills', orchestratorName),
    path.join(projectRoot, '.gemini', 'skills', orchestratorName)
  ];
  
  if (isGlobal) {
    const homeDir = process.env.USERPROFILE || process.env.HOME || process.env.HOMEPATH;
    if (homeDir) {
      const globalSkillDirs = [
        path.join(homeDir, '.codex', 'skills', orchestratorName),
        path.join(homeDir, '.agents', 'skills', orchestratorName),
        path.join(homeDir, '.claude', 'skills', orchestratorName),
        path.join(homeDir, '.gemini', 'config', 'skills', orchestratorName)
      ];
      globalSkillDirs.forEach(globalSkillsDir => dirDestinations.push(globalSkillsDir));
    }
  }

  dirDestinations.forEach(dest => {
    if (!fs.existsSync(dest)) fs.mkdirSync(dest, { recursive: true });
    writeText(path.join(dest, 'SKILL.md'), orchestratorContent);
  });

  if (!isDevRepo) {
    const singleRuleTargets = [
      path.join(projectRoot, '.cursor', 'rules', `${orchestratorName}.mdc`),
      path.join(projectRoot, '.windsurf', 'rules', `${orchestratorName}.md`),
      path.join(projectRoot, '.cline', 'rules', `${orchestratorName}.md`),
      path.join(projectRoot, '.roo', 'rules', `${orchestratorName}.md`),
      path.join(projectRoot, '.github', 'instructions', `${orchestratorName}.instructions.md`)
    ];
    singleRuleTargets.forEach(targetPath => {
      const dir = path.dirname(targetPath);
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
      writeText(targetPath, orchestratorContent);
    });
  }

  console.log(`  [OK] Master Orchestrator generated with ${allSkillData.length} skills in all IDE folders.`);
}

console.log('\nDone! All files and skills synced.\n');
