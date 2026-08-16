ALTER USER 'appuser'@'%' IDENTIFIED WITH mysql_native_password BY 'apppassword';
ALTER USER 'root'@'%' IDENTIFIED WITH mysql_native_password BY 'App!Password_2026';
FLUSH PRIVILEGES;