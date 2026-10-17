// Create (or update) an admin login.
// Usage: npm run seed:admin -- <email> <password> ["Display Name"]
import { db } from '../db.js';
import { hashPassword } from '../crypto.js';

const [, , email, password, displayName] = process.argv;

if (!email || !password) {
  console.error('Usage: npm run seed:admin -- <email> <password> ["Display Name"]');
  process.exit(1);
}

const hash = hashPassword(password);
const existing = db.prepare('SELECT id FROM users WHERE email = ?').get(email) as { id: number } | undefined;

if (existing) {
  db.prepare('UPDATE users SET password_hash = ?, display_name = ? WHERE email = ?').run(
    hash,
    displayName ?? '',
    email,
  );
  console.log(`Updated admin: ${email}`);
} else {
  db.prepare('INSERT INTO users (email, password_hash, display_name, role) VALUES (?, ?, ?, ?)').run(
    email,
    hash,
    displayName ?? '',
    'admin',
  );
  console.log(`Created admin: ${email}`);
}
