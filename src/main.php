<?php

/**
 * data types
 * primitives: int, bool, float -> scalar types
 * 
 * non primitives: object, string, array, class, interface -> compound types
 * 
 * iterable and traversable -> used to foreach as array
 * 
 * array in php serves as an array, map, order_map, set
 * 
 * never and void exist 
 *  never -> for functions that only exit() or throw something 
 *  void -> for functions that return nothing 
 */

$num = 5;

$bol = false;

$f_num = 5.5;

$name = "any name";

$array = array($num, $bol, $f_num, $name);

foreach($array as $item) {
    echo($item . " type: " . gettype($item) . PHP_EOL);
}
