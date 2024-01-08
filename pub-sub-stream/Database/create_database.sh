#!/bin/bash

# MySQL connection parameters
MYSQL_USER="root"
MYSQL_PASSWORD="root"
MYSQL_HOST="127.0.0.1"  # Use the Docker host IP address or container IP
MYSQL_PORT="3306"       # Use the mapped port on your host machine

# Create the database and table
mysql -h"$MYSQL_HOST" -P"$MYSQL_PORT" -u"$MYSQL_USER" -p"$MYSQL_PASSWORD" <<EOF
CREATE DATABASE IF NOT EXISTS Lookup;
USE Lookup;

CREATE TABLE IF NOT EXISTS Biography (
    Id VARCHAR(255) PRIMARY KEY,
    Description TEXT
);

EOF

echo "Database 'Lookup' and table 'Biography' created successfully."
