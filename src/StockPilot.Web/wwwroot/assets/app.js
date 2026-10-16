document.querySelectorAll('[data-confirm]').forEach(button => button.addEventListener('click', event => { if (!window.confirm(button.dataset.confirm)) event.preventDefault(); }));
document.querySelectorAll('nav a').forEach(a => { if (a.pathname === window.location.pathname && (!a.search || a.search === window.location.search)) a.setAttribute('aria-current', 'page'); });
document.querySelectorAll('[data-print]').forEach(button => button.addEventListener('click', () => window.print()));
