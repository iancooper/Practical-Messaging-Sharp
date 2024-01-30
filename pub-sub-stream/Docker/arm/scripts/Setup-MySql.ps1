$ErrorActionPreference = "Stop"

# MySQL container details
# TODO: you need to paste the container id for mysql here
$MYSQL_CONTAINER_NAME="22e0e0c339b6"
# MySQL server connection details
$MYSQL_USER="root"
$MYSQL_PASSWORD="root"
$MYSQL_HOST="127.0.0.1"  # Use the container's IP address
$MYSQL_PORT="3306"
# Database and table details
$DB_NAME="Lookup"
$TABLE_NAME="Biography"
# SQL commands to create database and table
$SQL_CREATE_DB="CREATE DATABASE IF NOT EXISTS $DB_NAME;"
$SQL_USE_DB="USE $DB_NAME;"
$SQL_CREATE_TABLE="CREATE TABLE IF NOT EXISTS $TABLE_NAME (
    Id VARCHAR(255) PRIMARY KEY,
    Description TEXT
);"
# Execute MySQL commands
docker exec -i $MYSQL_CONTAINER_NAME mysql -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" -h"$MYSQL_HOST" -P"$MYSQL_PORT" -e"$SQL_CREATE_DB $SQL_USE_DB $SQL_CREATE_TABLE"
echo "MySQL database '$DB_NAME' and table '$TABLE_NAME' created successfully."
