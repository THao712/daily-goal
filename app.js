const STORAGE_KEY = "daily-goal.tasks";
const COMPLETION_DELAY = 1350;

const palette = [
  "#f25f7a",
  "#f5b53f",
  "#35c88f",
  "#4d8df7",
  "#8f64ff",
  "#18c7d8",
  "#ff7a3d",
];

let combo = 0;
let lastCompletedAt = 0;
let comboTimer = 0;

const taskForm = document.querySelector("#taskForm");
const taskInput = document.querySelector("#taskInput");
const taskList = document.querySelector("#taskList");
const emptyState = document.querySelector("#emptyState");
const taskCount = document.querySelector("#taskCount");
const clearButton = document.querySelector("#clearButton");
const todayLabel = document.querySelector("#todayLabel");

let tasks = loadTasks();

todayLabel.textContent = new Intl.DateTimeFormat("zh-CN", {
  month: "long",
  day: "numeric",
  weekday: "short",
}).format(new Date());

taskForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const text = taskInput.value.trim();

  if (!text) {
    taskInput.value = "";
    return;
  }

  tasks.unshift({
    id: createId(),
    text,
    completed: false,
  });

  taskInput.value = "";
  saveTasks();
  renderTasks();
  taskInput.focus();
});

clearButton.addEventListener("click", () => {
  tasks = [];
  saveTasks();
  renderTasks();
});

taskList.addEventListener("click", (event) => {
  const item = event.target.closest(".task-item");
  if (!item) return;

  const id = item.dataset.id;

  if (event.target.matches(".delete-button")) {
    removeTask(id, item);
    return;
  }

  if (event.target.matches(".task-check")) {
    completeTask(id, item, event.target);
  }
});

renderTasks();

function renderTasks() {
  taskList.innerHTML = tasks
    .map(
      (task) => `
        <li class="task-item${task.completed ? " completed" : ""}" data-id="${task.id}">
          <input class="task-check" type="checkbox" aria-label="完成 ${escapeHtml(task.text)}" ${
            task.completed ? "checked" : ""
          } />
          <span class="task-text">${escapeHtml(task.text)}</span>
          <button class="delete-button" type="button" aria-label="删除 ${escapeHtml(task.text)}">×</button>
        </li>
      `,
    )
    .join("");

  const remaining = tasks.filter((task) => !task.completed).length;
  taskCount.textContent = `${remaining} 个待办`;
  emptyState.classList.toggle("hidden", tasks.length > 0);
  clearButton.classList.toggle("hidden", tasks.length === 0);
}

function completeTask(id, item, checkbox) {
  const task = tasks.find((entry) => entry.id === id);
  if (!task || task.completed) return;

  task.completed = true;
  checkbox.checked = true;
  item.classList.add("completed");
  saveTasks();
  launchParticles(checkbox, task.text);

  window.setTimeout(() => {
    removeTask(id, item);
  }, COMPLETION_DELAY);
}

function removeTask(id, item) {
  item.classList.add("leaving");

  window.setTimeout(() => {
    tasks = tasks.filter((task) => task.id !== id);
    saveTasks();
    renderTasks();
  }, 380);
}

function launchParticles(source, text) {
  const rect = source.getBoundingClientRect();
  const originX = rect.left + rect.width / 2;
  const originY = rect.top + rect.height / 2;
  const profile = buildParticleProfile(text);
  const now = Date.now();
  combo = now - lastCompletedAt < 1800 ? combo + 1 : 1;
  lastCompletedAt = now;
  window.clearTimeout(comboTimer);
  comboTimer = window.setTimeout(() => {
    combo = 0;
  }, 2200);

  if (combo > 1) {
    showCombo(combo);
  }

  const count = Math.min(44, Math.round(profile.count * (1 + Math.min(combo - 1, 3) * 0.18)));
  const shapes = ["dot", "spark", "star", "ring", "diamond"];

  for (let index = 0; index < count; index += 1) {
    const particle = document.createElement("span");
    const angle = (Math.PI * 2 * index) / count + random(-0.28, 0.28);
    const distance = random(62, profile.distance);
    const x = Math.cos(angle) * distance;
    const y = Math.sin(angle) * distance + random(-34, 16);
    const color = profile.palette[index % profile.palette.length];
    const shape = shapes[Math.floor(random(0, shapes.length))];

    particle.className = `particle ${shape === "dot" ? "" : shape}`;
    particle.style.left = `${originX}px`;
    particle.style.top = `${originY}px`;
    particle.style.setProperty("--x", `${x}px`);
    particle.style.setProperty("--y", `${y}px`);
    particle.style.setProperty("--mid-x", `${x * 0.58}px`);
    particle.style.setProperty("--mid-y", `${y * 0.5}px`);
    particle.style.setProperty("--gravity", `${random(54, 118)}px`);
    particle.style.setProperty("--particle-color", color);
    particle.style.setProperty("--particle-spin", `${random(-70, 70)}deg`);
    particle.style.setProperty("--life", `${random(820, 1240)}ms`);
    particle.style.animationDelay = `${random(0, 80)}ms`;

    document.body.appendChild(particle);
    particle.addEventListener("animationend", () => particle.remove(), { once: true });
  }
}

function buildParticleProfile(text) {
  const normalized = text.toLowerCase();
  const strong = /重要|最终|final|important|urgent|关键/.test(normalized);
  const sport = /运动|健身|跑步|workout|sport/.test(normalized);
  const rest = /睡觉|睡眠|休息|sleep|rest/.test(normalized);
  const lengthBonus = Math.min(10, Math.floor(text.length / 6));

  if (sport) {
    return {
      count: 24 + lengthBonus,
      distance: 160,
      palette: ["#ff3d57", "#ff7a3d", "#f5b53f", "#ffffff"],
    };
  }

  if (rest) {
    return {
      count: 21 + lengthBonus,
      distance: 148,
      palette: ["#4d8df7", "#18c7d8", "#8f64ff", "#eef7ff"],
    };
  }

  if (strong) {
    return {
      count: 30 + lengthBonus,
      distance: 176,
      palette: ["#f5b53f", "#f25f7a", "#8f64ff", "#35c88f", "#ffffff"],
    };
  }

  return {
    count: 22 + lengthBonus,
    distance: 146,
    palette,
  };
}

function showCombo(value) {
  const comboPop = document.createElement("span");
  comboPop.className = "combo-pop";
  comboPop.textContent = `Combo X${value}!`;
  document.body.appendChild(comboPop);
  comboPop.addEventListener("animationend", () => comboPop.remove(), { once: true });
}

function loadTasks() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "[]");
    return Array.isArray(saved) ? saved : [];
  } catch {
    return [];
  }
}

function saveTasks() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(tasks));
}

function random(min, max) {
  return Math.random() * (max - min) + min;
}

function createId() {
  if (window.crypto && typeof window.crypto.randomUUID === "function") {
    return window.crypto.randomUUID();
  }

  return `task-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function escapeHtml(value) {
  return value.replace(
    /[&<>"']/g,
    (character) =>
      ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&#039;",
      })[character],
  );
}
