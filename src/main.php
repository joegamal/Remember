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


class Base_Person {
    public static $name = "pop";

    public const name = "pop name";

    public function __construct(){
        echo "base constructor\n";
    }
} 



class Person extends Base_Person {


    public function __construct() {
        parent::__construct();
        echo "child constructor\n";
    }


    private $num2 = 10;

    public $num = 5;

    private const NUM = 50;

    public const PUB_NUM = 55;

    public function func() {
        echo "pop \n";
        echo $this->num . PHP_EOL;  //this for accessing class properties that defined normally 
    }

    public function get_num() {
        echo $this->num2 . PHP_EOL;
    }

    public function get_const() {
        return self::NUM;
    }
}

$obj = new Person;
$obj->func();
$obj->get_num();

echo $obj->get_const() . PHP_EOL;

echo Person::PUB_NUM . PHP_EOL;

echo Person::name . PHP_EOL;

